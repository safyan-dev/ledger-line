using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Tenancy.Infrastructure;
using PaymentReconciliation.Persistence;
using PaymentReconciliation.Tests.Integration.Infrastructure;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class SubscriptionTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task New_tenant_gets_free_plan_by_default()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync(NewSlug("subfree"));

        var subscription = await GetSubscriptionAsync(client, session);

        Assert.Equal("free", subscription.GetProperty("planCode").GetString());
        Assert.Equal("Active", subscription.GetProperty("status").GetString());
        Assert.Equal("Free", subscription.GetProperty("activationSource").GetString());
        Assert.True(subscription.GetProperty("paymentConfirmed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, subscription.GetProperty("currentPeriodEnd").ValueKind);
    }

    [Fact]
    public async Task Registration_with_selected_plan_creates_simulated_subscription()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync(NewSlug("subgrowth"), "growth");

        var subscription = await GetSubscriptionAsync(client, session);

        Assert.Equal("growth", subscription.GetProperty("planCode").GetString());
        Assert.Equal("Simulated", subscription.GetProperty("activationSource").GetString());
        Assert.False(subscription.GetProperty("paymentConfirmed").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, subscription.GetProperty("currentPeriodEnd").ValueKind);
    }

    [Fact]
    public async Task Upgrade_applies_immediately_and_changes_entitlements()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync(NewSlug("subupgrade"));

        var before = await GetEntitlementsAsync(client, session);
        var apiAccessBefore = FindFeature(before, "api_access");

        var upgraded = await ChangePlanAsync(client, session, "growth");
        Assert.Equal(HttpStatusCode.OK, upgraded.StatusCode);

        var subscription = await GetSubscriptionAsync(client, session);
        var after = await GetEntitlementsAsync(client, session);

        Assert.Equal("growth", subscription.GetProperty("planCode").GetString());
        Assert.False(apiAccessBefore.GetProperty("included").GetBoolean());
        Assert.True(FindFeature(after, "api_access").GetProperty("included").GetBoolean());
    }

    [Fact]
    public async Task Downgrade_is_scheduled_for_the_period_end()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync(NewSlug("subdown"), "growth");

        var response = await ChangePlanAsync(client, session, "free");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var subscription = await GetSubscriptionAsync(client, session);

        Assert.Equal("growth", subscription.GetProperty("planCode").GetString());
        Assert.Equal("free", subscription.GetProperty("pendingPlanCode").GetString());
        Assert.NotEqual(JsonValueKind.Null, subscription.GetProperty("pendingPlanEffectiveAt").ValueKind);
    }

    [Fact]
    public async Task Cancellation_keeps_the_plan_until_period_end()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync(NewSlug("subcancel"), "growth");

        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/subscription/cancel",
            session));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var subscription = await GetSubscriptionAsync(client, session);

        Assert.Equal("growth", subscription.GetProperty("planCode").GetString());
        Assert.True(subscription.GetProperty("cancelAtPeriodEnd").GetBoolean());
    }

    [Fact]
    public async Task Renewal_extends_the_period_and_is_idempotent()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync(NewSlug("subrenew"), "growth");
        var key = Guid.NewGuid().ToString("N");

        var first = await RenewAsync(client, session, key);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var second = await RenewAsync(client, session, key);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            firstBody.GetProperty("currentPeriodEnd").GetDateTimeOffset(),
            secondBody.GetProperty("currentPeriodEnd").GetDateTimeOffset());
    }

    [Fact]
    public async Task Expiry_sweep_expires_due_subscriptions_and_keeps_tenant_data()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync(NewSlug("subexpire"), "growth");
        var orderId = await client.CreateOrderAsync(session);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var past = DateTimeOffset.UtcNow.AddDays(-1);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tenancy.subscriptions SET current_period_end = {past} WHERE tenant_id = {session.TenantId}");
        }

        using (var scope = factory.Services.CreateScope())
        {
            var expiry = scope.ServiceProvider.GetRequiredService<SubscriptionExpiryService>();
            var expired = await expiry.ExpireDueAsync();
            Assert.True(expired >= 1);
        }

        var subscription = await GetSubscriptionAsync(client, session);
        Assert.Equal("Expired", subscription.GetProperty("status").GetString());

        var order = await client.SendAsync(client.Authorized(HttpMethod.Get, $"/api/orders/{orderId}", session));
        Assert.Equal(HttpStatusCode.OK, order.StatusCode);

        var blocked = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/orders",
            session,
            JsonContent.Create(new
            {
                customerName = "Expired Customer",
                customerReference = $"EXPIRED-{Guid.NewGuid():N}",
                expectedAmount = 25m,
                currency = "USD"
            })));
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    [Fact]
    public async Task Entitlements_reflect_plan_limits()
    {
        var client = factory.CreateClient();
        var free = await client.RegisterTenantAsync(NewSlug("entfree"));
        var growth = await client.RegisterTenantAsync(NewSlug("entgrowth"), "growth");

        var freeEntitlements = await GetEntitlementsAsync(client, free);
        var growthEntitlements = await GetEntitlementsAsync(client, growth);

        Assert.Equal(2, FindLimit(freeEntitlements, "team_members").GetProperty("value").GetInt32());
        Assert.Equal(5, FindLimit(growthEntitlements, "team_members").GetProperty("value").GetInt32());
        Assert.True(FindFeature(growthEntitlements, "csv_export").GetProperty("included").GetBoolean());
    }

    [Fact]
    public async Task Finance_cannot_change_the_subscription()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("subowner"));
        var member = await client.RegisterTenantAsync(NewSlug("submember"));

        var add = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/members",
            owner,
            JsonContent.Create(new { email = $"owner@{member.TenantSlug}.test", role = "Finance" })));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var response = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Put,
            "/api/tenants/current/subscription",
            member.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { planCode = "growth" })));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_plan_is_rejected()
    {
        var client = factory.CreateClient();

        var registration = await client.PostAsJsonAsync("/api/tenants/register", new
        {
            companyName = "Unknown Plan",
            tenantSlug = NewSlug("subunknown"),
            ownerName = "Unknown Owner",
            email = $"owner@{Guid.NewGuid():N}.test",
            password = "TenantPass123!",
            planCode = "does-not-exist"
        });
        Assert.Equal(HttpStatusCode.BadRequest, registration.StatusCode);

        var session = await client.RegisterTenantAsync(NewSlug("subbadchange"));
        var change = await ChangePlanAsync(client, session, "does-not-exist");
        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static async Task<JsonElement> GetSubscriptionAsync(HttpClient client, TestSession session)
    {
        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current/subscription",
            session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> GetEntitlementsAsync(HttpClient client, TestSession session)
    {
        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current/entitlements",
            session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> ChangePlanAsync(HttpClient client, TestSession session, string planCode)
        => client.SendAsync(client.Authorized(
            HttpMethod.Put,
            "/api/tenants/current/subscription",
            session,
            JsonContent.Create(new { planCode })));

    private static Task<HttpResponseMessage> RenewAsync(HttpClient client, TestSession session, string idempotencyKey)
        => client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/subscription/renew",
            session,
            JsonContent.Create(new { idempotencyKey })));

    private static JsonElement FindFeature(JsonElement entitlements, string key)
        => entitlements.GetProperty("features")
            .EnumerateArray()
            .First(feature => feature.GetProperty("key").GetString() == key);

    private static JsonElement FindLimit(JsonElement entitlements, string key)
        => entitlements.GetProperty("limits")
            .EnumerateArray()
            .First(limit => limit.GetProperty("key").GetString() == key);
}
