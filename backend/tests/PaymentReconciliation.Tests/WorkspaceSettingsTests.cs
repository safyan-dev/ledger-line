using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using PaymentReconciliation.Tests.Infrastructure;

namespace PaymentReconciliation.Tests;

[Collection(LedgerlineCollection.Name)]
public sealed class WorkspaceSettingsTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Owner_and_tenant_admin_can_update_workspace_settings()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("wssettings"));
        var admin = await AddMemberAsync(client, owner, "TenantAdmin");

        var updated = await UpdateAsync(client, owner, new
        {
            name = "Workspace Co",
            businessContactEmail = "billing@example.test",
            defaultCurrency = "eur",
            timeZone = "Europe/London"
        });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Workspace Co", body.GetProperty("name").GetString());
        Assert.Equal("billing@example.test", body.GetProperty("businessContactEmail").GetString());
        Assert.Equal("EUR", body.GetProperty("defaultCurrency").GetString());
        Assert.Equal("Europe/London", body.GetProperty("timeZone").GetString());

        var read = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/tenants/current", owner));
        var readBody = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Workspace Co", readBody.GetProperty("name").GetString());
        Assert.Equal("EUR", readBody.GetProperty("defaultCurrency").GetString());

        var adminUpdate = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Put,
            "/api/tenants/current",
            admin.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { defaultCurrency = "usd", timeZone = "UTC" })));

        Assert.Equal(HttpStatusCode.OK, adminUpdate.StatusCode);
        var adminBody = await adminUpdate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("USD", adminBody.GetProperty("defaultCurrency").GetString());
        Assert.Equal("UTC", adminBody.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task Finance_cannot_update_workspace_settings()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("wsfinance"));
        var finance = await AddMemberAsync(client, owner, "Finance");

        var response = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Put,
            "/api/tenants/current",
            finance.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { name = "Not Allowed" })));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_settings_are_rejected()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("wsinvalid"));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await UpdateAsync(client, owner, new { businessContactEmail = "not-an-email" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await UpdateAsync(client, owner, new { defaultCurrency = "EURO" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await UpdateAsync(client, owner, new { timeZone = "Mars/Olympus" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await UpdateAsync(client, owner, new { })).StatusCode);
    }

    [Fact]
    public async Task Settings_are_isolated_per_tenant()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("wsisoA"));
        var second = await client.RegisterTenantAsync(NewSlug("wsisoB"));

        await UpdateAsync(client, first, new
        {
            name = "First Workspace",
            businessContactEmail = "first@example.test",
            defaultCurrency = "gbp",
            timeZone = "Europe/London"
        });

        var read = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/tenants/current", second));
        var body = await read.Content.ReadFromJsonAsync<JsonElement>();

        Assert.EndsWith(" company", body.GetProperty("name").GetString());
        Assert.NotEqual("First Workspace", body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("businessContactEmail").ValueKind);
        Assert.Equal("USD", body.GetProperty("defaultCurrency").GetString());
        Assert.Equal("UTC", body.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task Contact_email_can_be_cleared()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("wsclear"));

        await UpdateAsync(client, owner, new { businessContactEmail = "clear@example.test" });

        var cleared = await UpdateAsync(client, owner, new { businessContactEmail = "" });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);

        var body = await cleared.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("businessContactEmail").ValueKind);
    }

    [Fact]
    public async Task Settings_update_is_audited()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("wsaudit"));

        await UpdateAsync(client, owner, new { defaultCurrency = "cad", timeZone = "America/Toronto" });

        var audit = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/audit-logs?module=tenancy&pageSize=50",
            owner));
        audit.EnsureSuccessStatusCode();

        var body = await audit.Content.ReadFromJsonAsync<JsonElement>();
        var actions = body.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("action").GetString())
            .ToList();

        Assert.Contains("tenant.settings_updated", actions);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static string Email(TestSession session)
        => $"owner@{session.TenantSlug}.test";

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client,
        TestSession session,
        object body)
        => client.SendAsync(client.Authorized(
            HttpMethod.Put,
            "/api/tenants/current",
            session,
            JsonContent.Create(body)));

    private async Task<TestSession> AddMemberAsync(HttpClient client, TestSession owner, string role)
    {
        var member = await client.RegisterTenantAsync(NewSlug("wsmember"));
        var add = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/members",
            owner,
            JsonContent.Create(new { email = Email(member), role })));
        add.EnsureSuccessStatusCode();

        return member with { TenantId = owner.TenantId, TenantSlug = owner.TenantSlug };
    }
}
