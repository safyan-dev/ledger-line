using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentReconciliation.Tests.Integration.Infrastructure;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class TenantManagementTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Owner_can_rename_and_read_settings()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("manage"));

        var settings = await client.SendAsync(
            client.Authorized(HttpMethod.Get, "/api/tenants/current", owner));
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        var rename = await client.SendAsync(client.Authorized(
            HttpMethod.Put,
            "/api/tenants/current",
            owner,
            JsonContent.Create(new { name = "Renamed Company" })));
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        var body = await rename.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed Company", body.GetProperty("name").GetString());

        var updated = await client.SendAsync(
            client.Authorized(HttpMethod.Get, "/api/tenants/current", owner));
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed Company", updatedBody.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Finance_member_can_read_but_not_manage()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("finowner"));
        var member = await client.RegisterTenantAsync(NewSlug("finmember"));

        var added = await AddMemberAsync(client, owner, MemberEmail(member), "Finance");
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        var settings = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Get, "/api/tenants/current", member.AccessToken, owner.TenantSlug));
        var members = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Get, "/api/tenants/current/members", member.AccessToken, owner.TenantSlug));
        var rename = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Put, "/api/tenants/current", member.AccessToken, owner.TenantSlug,
            JsonContent.Create(new { name = "Not Allowed" })));
        var add = await AddMemberAsync(client, member, "ghost@example.test", "Finance", owner.TenantSlug);

        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
    }

    [Fact]
    public async Task Owner_can_add_change_and_remove_members()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("crudowner"));
        var member = await client.RegisterTenantAsync(NewSlug("crudmember"));

        var added = await AddMemberAsync(client, owner, MemberEmail(member), "Finance");
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        var changed = await client.SendAsync(client.Authorized(
            HttpMethod.Patch,
            $"/api/tenants/current/members/{member.UserId}",
            owner,
            JsonContent.Create(new { role = "TenantAdmin" })));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var changedBody = await changed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TenantAdmin", changedBody.GetProperty("role").GetString());

        var removed = await client.SendAsync(client.Authorized(
            HttpMethod.Delete,
            $"/api/tenants/current/members/{member.UserId}",
            owner));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var afterRemoval = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Get, "/api/tenants/current", member.AccessToken, owner.TenantSlug));
        Assert.Equal(HttpStatusCode.Forbidden, afterRemoval.StatusCode);
    }

    [Fact]
    public async Task TenantAdmin_cannot_manage_owner_and_last_owner_is_protected()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("admA"));
        var admin = await client.RegisterTenantAsync(NewSlug("admB"));
        var third = await client.RegisterTenantAsync(NewSlug("admC"));

        var added = await AddMemberAsync(client, owner, MemberEmail(admin), "TenantAdmin");
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        var assignOwner = await AddMemberAsync(client, admin, MemberEmail(third), "Owner", owner.TenantSlug);
        var demoteOwner = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Patch,
            $"/api/tenants/current/members/{owner.UserId}",
            admin.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { role = "Finance" })));
        var removeSelf = await client.SendAsync(client.Authorized(
            HttpMethod.Delete,
            $"/api/tenants/current/members/{owner.UserId}",
            owner));
        var demoteSelf = await client.SendAsync(client.Authorized(
            HttpMethod.Patch,
            $"/api/tenants/current/members/{owner.UserId}",
            owner,
            JsonContent.Create(new { role = "Finance" })));

        Assert.Equal(HttpStatusCode.Forbidden, assignOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, demoteOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, removeSelf.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, demoteSelf.StatusCode);
    }

    [Fact]
    public async Task Duplicate_member_and_unknown_email_are_rejected()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("dupowner"));
        var member = await client.RegisterTenantAsync(NewSlug("dupmember"));

        var first = await AddMemberAsync(client, owner, MemberEmail(member), "Finance");
        var duplicate = await AddMemberAsync(client, owner, MemberEmail(member), "Finance");
        var unknown = await AddMemberAsync(client, owner, $"ghost-{Guid.NewGuid():N}@example.test", "Finance");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_member_access_is_rejected()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("crossA"));
        var second = await client.RegisterTenantAsync(NewSlug("crossB"));

        var read = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Get, "/api/tenants/current", first.AccessToken, second.TenantSlug));
        var add = await AddMemberAsync(client, first, MemberEmail(first), "Finance", second.TenantSlug);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
    }

    [Fact]
    public async Task Tenant_management_actions_are_audited()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("auditowner"));
        var member = await client.RegisterTenantAsync(NewSlug("auditmember"));

        await client.SendAsync(client.Authorized(
            HttpMethod.Put,
            "/api/tenants/current",
            owner,
            JsonContent.Create(new { name = "Audited Company" })));
        await AddMemberAsync(client, owner, MemberEmail(member), "Finance");
        await client.SendAsync(client.Authorized(
            HttpMethod.Patch,
            $"/api/tenants/current/members/{member.UserId}",
            owner,
            JsonContent.Create(new { role = "TenantAdmin" })));
        await client.SendAsync(client.Authorized(
            HttpMethod.Delete,
            $"/api/tenants/current/members/{member.UserId}",
            owner));

        var audit = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/audit-logs?module=tenancy&pageSize=50",
            owner));
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);

        var body = await audit.Content.ReadFromJsonAsync<JsonElement>();
        var actions = body.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("action").GetString())
            .ToList();

        Assert.Contains("tenant.renamed", actions);
        Assert.Contains("tenant.member_added", actions);
        Assert.Contains("tenant.member_role_changed", actions);
        Assert.Contains("tenant.member_removed", actions);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static string MemberEmail(TestSession session)
        => $"owner@{session.TenantSlug}.test";

    private static Task<HttpResponseMessage> AddMemberAsync(
        HttpClient client,
        TestSession actor,
        string email,
        string role,
        string? tenantSlug = null)
        => client.SendAsync(client.AuthorizedAs(
            HttpMethod.Post,
            "/api/tenants/current/members",
            actor.AccessToken,
            tenantSlug ?? actor.TenantSlug,
            JsonContent.Create(new { email, role })));
}
