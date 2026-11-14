using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using PaymentReconciliation.Tests.Infrastructure;

namespace PaymentReconciliation.Tests;

[Collection(LedgerlineCollection.Name)]
public sealed class RolePermissionTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Owner_has_full_tenant_access()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("permowner"));

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.Authorized(HttpMethod.Get, "/api/tenants/current/members", owner))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.Authorized(HttpMethod.Put, "/api/tenants/current", owner,
                JsonContent.Create(new { name = "Owner Renamed" })))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.Authorized(HttpMethod.Get, "/api/tenants/current/subscription", owner))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.Authorized(HttpMethod.Put, "/api/tenants/current/subscription", owner,
                JsonContent.Create(new { planCode = "growth" })))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.Authorized(HttpMethod.Get, "/api/tenants/current/entitlements", owner))).StatusCode);
    }

    [Fact]
    public async Task TenantAdmin_can_operate_but_cannot_change_the_subscription_or_owner()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("permadminowner"), "growth");
        var admin = await AddMemberAsync(client, owner, "TenantAdmin");
        var finance = await client.RegisterTenantAsync(NewSlug("permadminfinance"));

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Get, "/api/tenants/current/members", admin.AccessToken, owner.TenantSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Post, "/api/tenants/current/members", admin.AccessToken, owner.TenantSlug,
                JsonContent.Create(new { email = Email(finance), role = "Finance" })))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Put, "/api/tenants/current", admin.AccessToken, owner.TenantSlug,
                JsonContent.Create(new { name = "Admin Renamed" })))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Get, "/api/tenants/current/subscription", admin.AccessToken, owner.TenantSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Get, "/api/tenants/current/entitlements", admin.AccessToken, owner.TenantSlug))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Put, "/api/tenants/current/subscription", admin.AccessToken, owner.TenantSlug,
                JsonContent.Create(new { planCode = "growth" })))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Post, "/api/tenants/current/subscription/cancel", admin.AccessToken, owner.TenantSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Patch, $"/api/tenants/current/members/{owner.UserId}", admin.AccessToken, owner.TenantSlug,
                JsonContent.Create(new { role = "Finance" })))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Delete, $"/api/tenants/current/members/{owner.UserId}", admin.AccessToken, owner.TenantSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Post, "/api/tenants/current/members", admin.AccessToken, owner.TenantSlug,
                JsonContent.Create(new { email = Email(finance), role = "Owner" })))).StatusCode);
    }

    [Fact]
    public async Task Finance_can_read_but_cannot_manage()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("permfinowner"));
        var finance = await AddMemberAsync(client, owner, "Finance");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Get, "/api/tenants/current/members", finance.AccessToken, owner.TenantSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Get, "/api/tenants/current/subscription", finance.AccessToken, owner.TenantSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Get, "/api/tenants/current/entitlements", finance.AccessToken, owner.TenantSlug))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Put, "/api/tenants/current", finance.AccessToken, owner.TenantSlug,
                JsonContent.Create(new { name = "Not Allowed" })))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Post, "/api/tenants/current/members", finance.AccessToken, owner.TenantSlug,
                JsonContent.Create(new { email = "ghost@example.test", role = "Finance" })))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(
            client.AuthorizedAs(HttpMethod.Put, "/api/tenants/current/subscription", finance.AccessToken, owner.TenantSlug,
                JsonContent.Create(new { planCode = "growth" })))).StatusCode);
    }

    [Fact]
    public async Task Finance_and_tenant_admin_can_view_usage()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("permusageowner"), "growth");
        var admin = await AddMemberAsync(client, owner, "TenantAdmin");
        var finance = await AddMemberAsync(client, owner, "Finance");

        foreach (var session in new[] { admin, finance })
        {
            var usage = await client.SendAsync(client.AuthorizedAs(
                HttpMethod.Get,
                "/api/usage",
                session.AccessToken,
                owner.TenantSlug));
            var feature = await client.SendAsync(client.AuthorizedAs(
                HttpMethod.Get,
                "/api/usage/features/api_access",
                session.AccessToken,
                owner.TenantSlug));

            Assert.Equal(HttpStatusCode.OK, usage.StatusCode);
            Assert.Equal(HttpStatusCode.OK, feature.StatusCode);
        }
    }

    [Fact]
    public async Task Legacy_admin_role_input_maps_to_tenant_admin()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("permalias"));
        var member = await client.RegisterTenantAsync(NewSlug("permaliasmember"));

        var add = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/members",
            owner,
            JsonContent.Create(new { email = Email(member), role = "Admin" })));

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var body = await add.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TenantAdmin", body.GetProperty("role").GetString());
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static string Email(TestSession session)
        => $"owner@{session.TenantSlug}.test";

    private async Task<TestSession> AddMemberAsync(HttpClient client, TestSession owner, string role)
    {
        var member = await client.RegisterTenantAsync(NewSlug("permrole"));
        var add = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/members",
            owner,
            JsonContent.Create(new { email = Email(member), role })));
        add.EnsureSuccessStatusCode();

        return member with { TenantId = owner.TenantId, TenantSlug = owner.TenantSlug };
    }
}
