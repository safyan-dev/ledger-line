using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using PaymentReconciliation.Tests.Integration.Infrastructure;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class PlatformTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Platform_admin_can_login_without_a_workspace_and_list_tenants()
    {
        var client = factory.CreateClient();
        var tenant = await client.RegisterTenantAsync(NewSlug("platformlist"));
        var (email, password) = await factory.CreatePlatformAdminAsync();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("user").GetProperty("isPlatformAdmin").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("tenant").ValueKind);

        var accessToken = body.GetProperty("accessToken").GetString();
        var tenants = new HttpRequestMessage(HttpMethod.Get, "/api/platform/tenants");
        tenants.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var tenantsResponse = await client.SendAsync(tenants);

        Assert.Equal(HttpStatusCode.OK, tenantsResponse.StatusCode);
        var tenantsBody = await tenantsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            tenantsBody.EnumerateArray(),
            item => item.GetProperty("slug").GetString() == tenant.TenantSlug);
        Assert.Contains(
            tenantsBody.EnumerateArray(),
            item => item.GetProperty("slug").GetString() == tenant.TenantSlug
                    && item.GetProperty("ownerEmail").GetString() == $"owner@{tenant.TenantSlug}.test");
    }

    [Fact]
    public async Task Platform_admin_can_view_dashboard_and_manage_tenant_status()
    {
        var client = factory.CreateClient();
        var tenant = await client.RegisterTenantAsync(NewSlug("platformdetail"));
        var token = await GetPlatformAdminTokenAsync(client);

        var dashboard = await SendAsPlatformAdminAsync(client, token, HttpMethod.Get, "/api/platform/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        var dashboardBody = await dashboard.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dashboardBody.GetProperty("totalTenants").GetInt32() > 0);
        Assert.Contains(
            dashboardBody.GetProperty("recentRegistrations").EnumerateArray(),
            item => item.GetProperty("ownerEmail").GetString() == $"owner@{tenant.TenantSlug}.test");

        var detail = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Get,
            $"/api/platform/tenants/{tenant.TenantId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"owner@{tenant.TenantSlug}.test", detailBody.GetProperty("owner").GetProperty("email").GetString());
        Assert.Equal(1, detailBody.GetProperty("memberCount").GetInt32());
        Assert.Equal("free", detailBody.GetProperty("planCode").GetString());
        Assert.Equal(3, detailBody.GetProperty("usage").GetProperty("metrics").GetArrayLength());

        var suspended = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            $"/api/platform/tenants/{tenant.TenantId}/status",
            JsonContent.Create(new { status = "Suspended" }));
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        Assert.Equal(
            "Suspended",
            (await suspended.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var reactivated = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            $"/api/platform/tenants/{tenant.TenantId}/status",
            JsonContent.Create(new { status = "Active" }));
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_can_update_plan_and_subscription()
    {
        var client = factory.CreateClient();
        var tenant = await client.RegisterTenantAsync(NewSlug("platformsubscription"));
        var token = await GetPlatformAdminTokenAsync(client);

        var plansResponse = await SendAsPlatformAdminAsync(client, token, HttpMethod.Get, "/api/platform/plans");
        Assert.Equal(HttpStatusCode.OK, plansResponse.StatusCode);
        var plans = await plansResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(plans.EnumerateArray(), plan => plan.GetProperty("code").GetString() == "free");

        var featureUpdate = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            "/api/platform/plans/free/features/csv_export",
            JsonContent.Create(new { included = true }));
        Assert.Equal(HttpStatusCode.OK, featureUpdate.StatusCode);
        var featureReset = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            "/api/platform/plans/free/features/csv_export",
            JsonContent.Create(new { included = false }));
        Assert.Equal(HttpStatusCode.OK, featureReset.StatusCode);

        var limitUpdate = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            "/api/platform/plans/free/limits/orders_per_month",
            JsonContent.Create(new { value = 600 }));
        Assert.Equal(HttpStatusCode.OK, limitUpdate.StatusCode);
        var limitReset = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            "/api/platform/plans/free/limits/orders_per_month",
            JsonContent.Create(new { value = 500 }));
        Assert.Equal(HttpStatusCode.OK, limitReset.StatusCode);

        var subscriptionsResponse = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Get,
            "/api/platform/subscriptions?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, subscriptionsResponse.StatusCode);
        var subscriptions = await subscriptionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var subscriptionId = subscriptions.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("tenantId").GetGuid() == tenant.TenantId)
            .GetProperty("id").GetGuid();

        var planChange = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Put,
            $"/api/platform/subscriptions/{subscriptionId}/plan",
            JsonContent.Create(new { planCode = "growth" }));
        Assert.Equal(HttpStatusCode.OK, planChange.StatusCode);
        var changed = await planChange.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("growth", changed.GetProperty("planCode").GetString());
        Assert.Equal("Manual", changed.GetProperty("activationSource").GetString());
        Assert.False(changed.GetProperty("paymentConfirmed").GetBoolean());

        var extended = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/platform/subscriptions/{subscriptionId}/extend",
            JsonContent.Create(new { days = 14 }));
        Assert.Equal(HttpStatusCode.OK, extended.StatusCode);

        var suspended = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            $"/api/platform/subscriptions/{subscriptionId}/status",
            JsonContent.Create(new { status = "Suspended" }));
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);

        var reactivated = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            $"/api/platform/subscriptions/{subscriptionId}/status",
            JsonContent.Create(new { status = "Active" }));
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_can_view_and_reply_to_tenant_support_tickets()
    {
        var client = factory.CreateClient();
        var tenant = await client.RegisterTenantAsync(NewSlug("platformsupport"));
        var createTicket = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/support/tickets",
            tenant,
            JsonContent.Create(new
            {
                subject = "Need help with billing",
                description = "Please review our billing setup.",
                category = "BillingIssue",
                priority = "High"
            })));
        Assert.Equal(HttpStatusCode.Created, createTicket.StatusCode);
        var ticketId = (await createTicket.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var token = await GetPlatformAdminTokenAsync(client);

        var list = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Get,
            "/api/platform/support/tickets?category=BillingIssue&priority=High");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            listBody.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == ticketId
                    && item.GetProperty("tenantName").GetString() == $"{tenant.TenantSlug} company");

        var detail = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Get,
            $"/api/platform/support/tickets/{ticketId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var reply = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Post,
            $"/api/platform/support/tickets/{ticketId}/messages",
            JsonContent.Create(new { body = "We will review your billing configuration." }));
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        var replyBody = await reply.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PlatformAdmin", replyBody.GetProperty("authorRole").GetString());

        var status = await SendAsPlatformAdminAsync(
            client,
            token,
            HttpMethod.Patch,
            $"/api/platform/support/tickets/{ticketId}/status",
            JsonContent.Create(new { status = "InProgress" }));
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    [Fact]
    public async Task Tenant_member_cannot_call_platform_administration_endpoints()
    {
        var client = factory.CreateClient();
        var tenant = await client.RegisterTenantAsync(NewSlug("platformdenied"));
        var paths = new[]
        {
            "/api/platform/dashboard",
            "/api/platform/tenants",
            $"/api/platform/tenants/{tenant.TenantId}",
            "/api/platform/plans",
            "/api/platform/subscriptions",
            "/api/platform/support/tickets"
        };
        foreach (var path in paths)
        {
            var response = await client.SendAsync(client.Authorized(HttpMethod.Get, path, tenant));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Tenant_login_rejects_a_workspace_the_user_does_not_belong_to()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("platformwrongA"));
        var second = await client.RegisterTenantAsync(NewSlug("platformwrongB"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"owner@{first.TenantSlug}.test",
            password = "TenantPass123!",
            tenantSlug = second.TenantSlug
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.tenant_membership_required", body.GetProperty("code").GetString());
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private async Task<string> GetPlatformAdminTokenAsync(HttpClient client)
    {
        var (email, password) = await factory.CreatePlatformAdminAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsPlatformAdminAsync(
        HttpClient client,
        string token,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
