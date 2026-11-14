using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using PaymentReconciliation.Tests.Infrastructure;

namespace PaymentReconciliation.Tests;

[Collection(LedgerlineCollection.Name)]
public sealed class CrossTenantSecurityTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task New_endpoints_reject_non_members()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("xtenantA"));
        var second = await client.RegisterTenantAsync(NewSlug("xtenantB"));

        var paths = new[]
        {
            "/api/usage",
            "/api/usage/features/api_access",
            "/api/tenants/current",
            "/api/tenants/current/invitations",
            "/api/tenants/current/subscription/history",
            "/api/support/tickets"
        };

        foreach (var path in paths)
        {
            var response = await client.SendAsync(client.AuthorizedAs(
                HttpMethod.Get,
                path,
                second.AccessToken,
                first.TenantSlug));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Usage_is_tenant_scoped()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("xusageA"));
        var second = await client.RegisterTenantAsync(NewSlug("xusageB"));

        await client.CreateOrderAsync(first);
        await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/payments",
            first,
            JsonContent.Create(new
            {
                amount = 15,
                currency = "USD",
                customerName = "Tenant A payment",
                reference = $"XPAY-{Guid.NewGuid():N}"
            })));

        var firstUsage = await GetUsageAsync(client, first);
        var secondUsage = await GetUsageAsync(client, second);

        Assert.Equal(1, FindMetric(firstUsage, "OrdersPerMonth").GetProperty("used").GetInt32());
        Assert.Equal(1, FindMetric(firstUsage, "PaymentsPerMonth").GetProperty("used").GetInt32());
        Assert.Equal(0, FindMetric(secondUsage, "OrdersPerMonth").GetProperty("used").GetInt32());
        Assert.Equal(0, FindMetric(secondUsage, "PaymentsPerMonth").GetProperty("used").GetInt32());
        Assert.Equal(1, FindMetric(secondUsage, "TeamMembers").GetProperty("used").GetInt32());
    }

    [Fact]
    public async Task Subscription_history_is_tenant_scoped()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("xhistoryA"), "growth");
        var second = await client.RegisterTenantAsync(NewSlug("xhistoryB"));

        await client.SendAsync(client.Authorized(
            HttpMethod.Put,
            "/api/tenants/current/subscription",
            first,
            JsonContent.Create(new { planCode = "free" })));

        var firstHistory = await GetHistoryAsync(client, first);
        var secondHistory = await GetHistoryAsync(client, second);

        Assert.True(firstHistory.GetProperty("totalCount").GetInt32() >= 2);
        Assert.Equal(1, secondHistory.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Workspace_settings_ignore_tenant_ids_in_the_body()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("xsettingsA"));
        var second = await client.RegisterTenantAsync(NewSlug("xsettingsB"));

        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Put,
            "/api/tenants/current",
            first,
            JsonContent.Create(new
            {
                name = "Tenant A Workspace",
                defaultCurrency = "EUR",
                tenantId = second.TenantId
            })));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var firstSettings = await GetSettingsAsync(client, first);
        var secondSettings = await GetSettingsAsync(client, second);

        Assert.Equal("Tenant A Workspace", firstSettings.GetProperty("name").GetString());
        Assert.Equal("EUR", firstSettings.GetProperty("defaultCurrency").GetString());
        Assert.NotEqual("Tenant A Workspace", secondSettings.GetProperty("name").GetString());
        Assert.Equal("USD", secondSettings.GetProperty("defaultCurrency").GetString());
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static JsonElement FindMetric(JsonElement usage, string key)
        => usage.GetProperty("metrics")
            .EnumerateArray()
            .First(metric => metric.GetProperty("key").GetString() == key);

    private static async Task<JsonElement> GetUsageAsync(HttpClient client, TestSession session)
    {
        var response = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/usage", session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> GetHistoryAsync(HttpClient client, TestSession session)
    {
        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current/subscription/history",
            session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> GetSettingsAsync(HttpClient client, TestSession session)
    {
        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current",
            session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
