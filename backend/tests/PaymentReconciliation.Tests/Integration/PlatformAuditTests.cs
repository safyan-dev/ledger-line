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
public sealed class PlatformAuditTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Tenant_status_changes_are_audited_with_the_platform_actor()
    {
        var client = factory.CreateClient();
        var tenant = await client.RegisterTenantAsync(NewSlug("audittenant"));
        var platformToken = await GetPlatformTokenAsync(client);

        var suspended = await SendAsPlatformAsync(
            client,
            platformToken,
            HttpMethod.Patch,
            $"/api/platform/tenants/{tenant.TenantId}/status",
            JsonContent.Create(new { status = "Suspended" }));
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);

        var audit = await client.SendAsync(PlatformRequest(
            HttpMethod.Get,
            $"/api/platform/audit-logs?action=platform.tenant.status_changed&targetId={tenant.TenantId}",
            platformToken));
        audit.EnsureSuccessStatusCode();
        var body = await audit.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal("Tenant", item.GetProperty("targetType").GetString());
        Assert.Equal(tenant.TenantId, item.GetProperty("targetId").GetGuid());
        Assert.Equal("PlatformAdmin", item.GetProperty("actorRole").GetString());
        Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("actorName").GetString()));
        Assert.Contains(
            "Suspended",
            item.GetProperty("metadata").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Manual_subscription_status_changes_are_audited()
    {
        var client = factory.CreateClient();
        var tenant = await client.RegisterTenantAsync(NewSlug("auditsub"));
        var subscriptionId = await GetSubscriptionIdAsync(tenant.TenantId);
        var platformToken = await GetPlatformTokenAsync(client);

        var suspended = await SendAsPlatformAsync(
            client,
            platformToken,
            HttpMethod.Patch,
            $"/api/platform/subscriptions/{subscriptionId}/status",
            JsonContent.Create(new { status = "Suspended" }));
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);

        var audit = await client.SendAsync(PlatformRequest(
            HttpMethod.Get,
            $"/api/platform/audit-logs?action=platform.subscription.status_changed&targetId={subscriptionId}",
            platformToken));
        audit.EnsureSuccessStatusCode();
        var body = await audit.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal("Subscription", item.GetProperty("targetType").GetString());
        Assert.Equal(subscriptionId, item.GetProperty("targetId").GetGuid());
        Assert.Equal(tenant.TenantId, item.GetProperty("tenantId").GetGuid());
        Assert.Equal("PlatformAdmin", item.GetProperty("actorRole").GetString());
    }

    [Fact]
    public async Task Tenant_members_cannot_read_platform_audit_records()
    {
        var client = factory.CreateClient();
        var tenant = await client.RegisterTenantAsync(NewSlug("auditdenied"));

        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/platform/audit-logs",
            tenant));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        => client.SendAsync(PlatformRequest(method, path, token, content));

    private static HttpRequestMessage PlatformRequest(
        HttpMethod method,
        string path,
        string token,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}
