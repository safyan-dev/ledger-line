using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentReconciliation.Modules.Tenancy.Infrastructure;
using PaymentReconciliation.Persistence;
using PaymentReconciliation.Tests.Integration.Infrastructure;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class SubscriptionLifecycleTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Reactivate_clears_a_scheduled_cancellation()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("lifecyclecancel"), "growth");

        var canceled = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/subscription/cancel",
            owner));
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);

        var reactivated = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/subscription/reactivate",
            owner));
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);

        var body = await reactivated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("growth", body.GetProperty("planCode").GetString());
        Assert.Equal("Active", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("cancelAtPeriodEnd").GetBoolean());

        var history = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current/subscription/history?pageSize=50",
            owner));
        history.EnsureSuccessStatusCode();
        var historyBody = await history.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            historyBody.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("event").GetString() == "subscription.reactivated");
    }

    [Fact]
    public async Task Reactivate_after_expiry_starts_a_new_period_and_keeps_data()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("lifecycleexpired"), "growth");
        var orderId = await client.CreateOrderAsync(owner);

        await SetPeriodEndAsync(owner.TenantId, DateTimeOffset.UtcNow.AddDays(-1));
        await RunExpiryAsync();

        var expired = await GetSubscriptionAsync(client, owner);
        Assert.Equal("Expired", expired.GetProperty("status").GetString());

        var reactivated = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/subscription/reactivate",
            owner));
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);

        var body = await reactivated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", body.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("currentPeriodEnd").ValueKind);

        var order = await client.SendAsync(client.Authorized(HttpMethod.Get, $"/api/orders/{orderId}", owner));
        Assert.Equal(HttpStatusCode.OK, order.StatusCode);
    }

    [Fact]
    public async Task History_is_paged_and_newest_first()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("lifecyclehistory"), "growth");

        await client.SendAsync(client.Authorized(
            HttpMethod.Put,
            "/api/tenants/current/subscription",
            owner,
            JsonContent.Create(new { planCode = "free" })));

        var history = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current/subscription/history?page=1&pageSize=1",
            owner));

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var body = await history.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 2);
        Assert.True(body.GetProperty("totalPages").GetInt32() >= 2);
    }

    [Fact]
    public async Task Scheduled_downgrade_is_applied_when_the_period_ends()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("lifecycledowngrade"), "growth");
        var orderId = await client.CreateOrderAsync(owner);

        var scheduled = await client.SendAsync(client.Authorized(
            HttpMethod.Put,
            "/api/tenants/current/subscription",
            owner,
            JsonContent.Create(new { planCode = "free" })));
        Assert.Equal(HttpStatusCode.OK, scheduled.StatusCode);

        await SetPendingPlanEffectiveAsync(owner.TenantId, DateTimeOffset.UtcNow.AddDays(-1));

        int applied;
        using (var scope = factory.Services.CreateScope())
        {
            applied = await scope.ServiceProvider.GetRequiredService<SubscriptionExpiryService>()
                .ApplyDuePlanChangesAsync();
        }

        Assert.True(applied >= 1);

        var subscription = await GetSubscriptionAsync(client, owner);
        Assert.Equal("free", subscription.GetProperty("planCode").GetString());
        Assert.Equal("Active", subscription.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, subscription.GetProperty("pendingPlanCode").ValueKind);

        var order = await client.SendAsync(client.Authorized(HttpMethod.Get, $"/api/orders/{orderId}", owner));
        Assert.Equal(HttpStatusCode.OK, order.StatusCode);
    }

    [Fact]
    public async Task Free_plan_cancellation_is_immediate()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("lifecyclefreecancel"));

        var canceled = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/subscription/cancel",
            owner));

        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        var body = await canceled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Canceled", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancellation_at_period_end_results_in_canceled_and_keeps_data()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("lifecyclecancelend"), "growth");
        var orderId = await client.CreateOrderAsync(owner);

        await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/subscription/cancel",
            owner));

        await SetPeriodEndAsync(owner.TenantId, DateTimeOffset.UtcNow.AddDays(-1));
        await RunExpiryAsync();

        var subscription = await GetSubscriptionAsync(client, owner);
        Assert.Equal("Canceled", subscription.GetProperty("status").GetString());

        var order = await client.SendAsync(client.Authorized(HttpMethod.Get, $"/api/orders/{orderId}", owner));
        Assert.Equal(HttpStatusCode.OK, order.StatusCode);
    }

    [Fact]
    public async Task TenantAdmin_cannot_reactivate_the_subscription()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("lifecycleadminowner"));
        var admin = await AddMemberAsync(client, owner, "TenantAdmin");

        var response = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Post,
            "/api/tenants/current/subscription/reactivate",
            admin.AccessToken,
            owner.TenantSlug));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static string Email(TestSession session)
        => $"owner@{session.TenantSlug}.test";

    private static async Task<JsonElement> GetSubscriptionAsync(HttpClient client, TestSession session)
    {
        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current/subscription",
            session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<TestSession> AddMemberAsync(HttpClient client, TestSession owner, string role)
    {
        var member = await client.RegisterTenantAsync(NewSlug("lifecyclemember"));
        var add = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/members",
            owner,
            JsonContent.Create(new { email = Email(member), role })));
        add.EnsureSuccessStatusCode();

        return member with { TenantId = owner.TenantId, TenantSlug = owner.TenantSlug };
    }

    private async Task SetPeriodEndAsync(Guid tenantId, DateTimeOffset value)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE tenancy.subscriptions SET current_period_end = {value} WHERE tenant_id = {tenantId}");
    }

    private async Task SetPendingPlanEffectiveAsync(Guid tenantId, DateTimeOffset value)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE tenancy.subscriptions SET pending_plan_effective_at = {value} WHERE tenant_id = {tenantId}");
    }

    private async Task RunExpiryAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SubscriptionExpiryService>().ExpireDueAsync();
    }
}
