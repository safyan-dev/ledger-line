using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using PaymentReconciliation.Tests.Infrastructure;

namespace PaymentReconciliation.Tests;

[Collection(LedgerlineCollection.Name)]
public sealed class AccountTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Profile_can_be_read_and_updated()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("acctprofile"));

        var updated = await client.SendAsync(client.Authorized(
            HttpMethod.Patch,
            "/api/account/profile",
            owner,
            JsonContent.Create(new { displayName = "Updated Owner" })));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Owner", updatedBody.GetProperty("displayName").GetString());

        var profile = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/account/profile", owner));
        profile.EnsureSuccessStatusCode();
        var profileBody = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Owner", profileBody.GetProperty("displayName").GetString());
        Assert.True(profileBody.GetProperty("emailConfirmed").GetBoolean());

        var invalid = await client.SendAsync(client.Authorized(
            HttpMethod.Patch,
            "/api/account/profile",
            owner,
            JsonContent.Create(new { displayName = "   " })));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Change_email_verifies_and_revokes_sessions()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("acctemail"));
        var oldEmail = $"owner@{owner.TenantSlug}.test";
        var newEmail = $"changed-{Guid.NewGuid():N}@example.test";

        var requested = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/account/change-email",
            owner,
            JsonContent.Create(new { newEmail })));

        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var requestedBody = await requested.Content.ReadFromJsonAsync<JsonElement>();
        var token = requestedBody.GetProperty("verificationToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var confirmed = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/account/change-email/confirm",
            owner,
            JsonContent.Create(new { newEmail, token })));

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var confirmedBody = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(newEmail, confirmedBody.GetProperty("email").GetString());

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = owner.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        var loginNew = await client.LoginAsync(newEmail, owner.TenantId, owner.TenantSlug);
        Assert.False(string.IsNullOrWhiteSpace(loginNew.AccessToken));

        var loginOld = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = oldEmail,
            password = "TenantPass123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginOld.StatusCode);
    }

    [Fact]
    public async Task Change_email_rejects_an_existing_address()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("acctdupe"));
        var other = await client.RegisterTenantAsync(NewSlug("acctother"));

        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/account/change-email",
            owner,
            JsonContent.Create(new { newEmail = $"owner@{other.TenantSlug}.test" })));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Change_email_rejects_an_invalid_confirmation_token()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("accttoken"));
        var newEmail = $"bad-token-{Guid.NewGuid():N}@example.test";

        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/account/change-email/confirm",
            owner,
            JsonContent.Create(new { newEmail, token = "not-a-real-token" })));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registration_rejects_an_invalid_email()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "TenantPass123!",
            displayName = "Bad Email"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}
