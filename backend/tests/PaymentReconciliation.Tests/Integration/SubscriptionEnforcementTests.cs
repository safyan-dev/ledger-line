using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence;
using PaymentReconciliation.Tests.Integration.Infrastructure;
using Xunit;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class SubscriptionEnforcementTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Suspended_subscription_is_read_only_until_reactivated()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("subenforce"));
        await client.CreateOrderAsync(owner);
        var subscriptionId = await GetSubscriptionIdAsync(owner.TenantId);
        var platformToken = await GetPlatformTokenAsync(client);

        var suspended = await SendAsPlatformAsync(
            client,
            platformToken,
            HttpMethod.Patch,
            $"/api/platform/subscriptions/{subscriptionId}/status",
            JsonContent.Create(new { status = "Suspended" }));

        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);

        var read = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/orders", owner));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/orders",
            owner,
            JsonContent.Create(new
            {
                customerName = "Blocked Customer",
                customerReference = $"BLOCKED-{Guid.NewGuid():N}",
                expectedAmount = 25m,
                currency = "USD"
            })));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        var writeBody = await write.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("subscription.inactive", writeBody.GetProperty("code").GetString());

        var webhook = await WebhookTestClient.SendSignedAsync(
            client,
            owner.TenantSlug,
            WebhookTestClient.PaymentPayload(
                WebhookTestClient.NewEventId(),
                WebhookTestClient.NewProviderPaymentId(),
                25m));
        Assert.Equal(HttpStatusCode.Forbidden, webhook.StatusCode);

        var subscription = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current/subscription",
            owner));
        Assert.Equal(HttpStatusCode.OK, subscription.StatusCode);

        var selfReactivate = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/subscription/reactivate",
            owner));
        Assert.Equal(HttpStatusCode.Forbidden, selfReactivate.StatusCode);
        var selfReactivateBody = await selfReactivate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("subscription.suspended", selfReactivateBody.GetProperty("code").GetString());

        var activated = await SendAsPlatformAsync(
            client,
            platformToken,
            HttpMethod.Patch,
            $"/api/platform/subscriptions/{subscriptionId}/status",
            JsonContent.Create(new { status = "Active" }));
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);

        await client.CreateOrderAsync(owner);
    }

    private async Task<Guid> GetSubscriptionIdAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Set<Subscription>()
            .IgnoreQueryFilters()
            .Where(subscription => subscription.TenantId == tenantId)
            .Select(subscription => subscription.Id)
            .SingleAsync();
    }

    private async Task<string> GetPlatformTokenAsync(HttpClient client)
    {
        var (email, password) = await factory.CreatePlatformAdminAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private static Task<HttpResponseMessage> SendAsPlatformAsync(
        HttpClient client,
        string token,
        HttpMethod method,
        string path,
        HttpContent content)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}
