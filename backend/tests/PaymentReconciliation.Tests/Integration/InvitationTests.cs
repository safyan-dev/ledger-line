using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentReconciliation.Persistence;
using PaymentReconciliation.Tests.Integration.Infrastructure;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class InvitationTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Owner_can_invite_and_accept_a_new_account()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("invnew"));
        var email = $"invitee-{Guid.NewGuid():N}@example.test";

        var created = await InviteAsync(client, owner, email, "Finance");
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var invitation = createdBody.GetProperty("invitation");
        Assert.Equal(email, invitation.GetProperty("email").GetString());
        Assert.Equal("Finance", invitation.GetProperty("role").GetString());
        Assert.Equal("Pending", invitation.GetProperty("status").GetString());

        var token = TestEmailSender.LatestToken(email, "Invitation to");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var accepted = await client.PostAsJsonAsync("/api/invitations/accept", new
        {
            token,
            displayName = "Invited Person",
            password = "InvitePass123!"
        });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var acceptedBody = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(owner.TenantSlug, acceptedBody.GetProperty("tenantSlug").GetString());
        Assert.Equal("Finance", acceptedBody.GetProperty("role").GetString());
        Assert.NotEqual(JsonValueKind.Null, acceptedBody.GetProperty("session").ValueKind);

        var accessToken = acceptedBody.GetProperty("session").GetProperty("accessToken").GetString();
        var profile = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        profile.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await client.SendAsync(profile);
        profileResponse.EnsureSuccessStatusCode();
        var profileBody = await profileResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, profileBody.GetProperty("email").GetString());

        var members = await client.SendAsync(
            client.Authorized(HttpMethod.Get, "/api/tenants/current/members", owner));
        var membersBody = await members.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            membersBody.EnumerateArray(),
            item => item.GetProperty("email").GetString() == email);
    }

    [Fact]
    public async Task Existing_account_must_sign_in_before_accepting()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("invexistingowner"));
        var existing = await client.RegisterTenantAsync(NewSlug("invexistingmember"));
        var email = Email(existing);

        var created = await InviteAsync(client, owner, email, "Finance");
        var token = TestEmailSender.LatestToken(email, "Invitation to");

        var anonymous = await client.PostAsJsonAsync("/api/invitations/accept", new { token });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var authenticated = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Post,
            "/api/invitations/accept",
            existing.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { token })));

        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        var body = await authenticated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("session").ValueKind);
    }

    [Fact]
    public async Task TenantAdmin_can_invite_finance_but_not_owner_and_finance_cannot_invite()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("invrolesowner"), "growth");
        var admin = await AddMemberAsync(client, owner, "TenantAdmin");
        var finance = await AddMemberAsync(client, owner, "Finance");

        var financeInvite = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Post,
            "/api/tenants/current/invitations",
            admin.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { email = $"finance-{Guid.NewGuid():N}@example.test", role = "Finance" })));
        Assert.Equal(HttpStatusCode.OK, financeInvite.StatusCode);

        var ownerInvite = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Post,
            "/api/tenants/current/invitations",
            admin.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { email = $"owner-{Guid.NewGuid():N}@example.test", role = "Owner" })));
        Assert.Equal(HttpStatusCode.Forbidden, ownerInvite.StatusCode);

        var financeAttempt = await client.SendAsync(client.AuthorizedAs(
            HttpMethod.Post,
            "/api/tenants/current/invitations",
            finance.AccessToken,
            owner.TenantSlug,
            JsonContent.Create(new { email = $"other-{Guid.NewGuid():N}@example.test", role = "Finance" })));
        Assert.Equal(HttpStatusCode.Forbidden, financeAttempt.StatusCode);
    }

    [Fact]
    public async Task Invitation_token_is_single_use()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("invsingle"));
        var email = $"single-{Guid.NewGuid():N}@example.test";
        var created = await InviteAsync(client, owner, email, "Finance");
        var token = TestEmailSender.LatestToken(email, "Invitation to");

        var first = await client.PostAsJsonAsync("/api/invitations/accept", new
        {
            token,
            displayName = "Single Use",
            password = "InvitePass123!"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/invitations/accept", new
        {
            token,
            displayName = "Single Use",
            password = "InvitePass123!"
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Expired_invitation_is_rejected()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("invexpired"));
        var email = $"expired-{Guid.NewGuid():N}@example.test";
        var created = await InviteAsync(client, owner, email, "Finance");
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = createdBody.GetProperty("invitation").GetProperty("id").GetGuid();
        var token = TestEmailSender.LatestToken(email, "Invitation to");

        await ExpireInvitationAsync(invitationId);

        var accepted = await client.PostAsJsonAsync("/api/invitations/accept", new
        {
            token,
            displayName = "Expired Person",
            password = "InvitePass123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, accepted.StatusCode);
    }

    [Fact]
    public async Task Revoked_invitation_is_rejected_and_resend_rotates_the_token()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("invrevoke"));
        var email = $"revoke-{Guid.NewGuid():N}@example.test";

        var created = await InviteAsync(client, owner, email, "Finance");
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = createdBody.GetProperty("invitation").GetProperty("id").GetGuid();
        var revokedToken = TestEmailSender.LatestToken(email, "Invitation to");

        var revoked = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            $"/api/tenants/current/invitations/{invitationId}/revoke",
            owner));
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

        var revokedAccept = await client.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = revokedToken,
            displayName = "Revoked Person",
            password = "InvitePass123!"
        });
        Assert.Equal(HttpStatusCode.Conflict, revokedAccept.StatusCode);

        var rotateEmail = $"rotate-{Guid.NewGuid():N}@example.test";
        var second = await InviteAsync(client, owner, rotateEmail, "Finance");
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        var secondId = secondBody.GetProperty("invitation").GetProperty("id").GetGuid();
        var oldToken = TestEmailSender.LatestToken(rotateEmail, "Invitation to");

        var resent = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            $"/api/tenants/current/invitations/{secondId}/resend",
            owner));
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);

        var newToken = TestEmailSender.LatestToken(rotateEmail, "Invitation to");
        Assert.NotEqual(oldToken, newToken);

        var oldTokenAccept = await client.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = oldToken,
            displayName = "Old Token",
            password = "InvitePass123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, oldTokenAccept.StatusCode);

        var newTokenAccept = await client.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = newToken,
            displayName = "New Token",
            password = "InvitePass123!"
        });
        Assert.Equal(HttpStatusCode.OK, newTokenAccept.StatusCode);
    }

    [Fact]
    public async Task Invitations_are_tenant_scoped()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("invscopeA"));
        var second = await client.RegisterTenantAsync(NewSlug("invscopeB"));

        var created = await InviteAsync(client, first, $"scoped-{Guid.NewGuid():N}@example.test", "Finance");
        var invitationId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("invitation")
            .GetProperty("id")
            .GetGuid();

        var listed = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/tenants/current/invitations",
            second));
        var listedBody = await listed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(listedBody.GetProperty("items").EnumerateArray());

        var revoke = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            $"/api/tenants/current/invitations/{invitationId}/revoke",
            second));
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }

    [Fact]
    public async Task Duplicate_pending_invitation_is_rejected()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("invdup"));
        var email = $"dup-{Guid.NewGuid():N}@example.test";

        var first = await InviteAsync(client, owner, email, "Finance");
        var second = await InviteAsync(client, owner, email, "Finance");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static string Email(TestSession session)
        => $"owner@{session.TenantSlug}.test";

    private static Task<HttpResponseMessage> InviteAsync(
        HttpClient client,
        TestSession session,
        string email,
        string role)
        => client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/invitations",
            session,
            JsonContent.Create(new { email, role })));

    private async Task<TestSession> AddMemberAsync(HttpClient client, TestSession owner, string role)
    {
        var member = await client.RegisterTenantAsync(NewSlug("invmember"));
        var add = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/tenants/current/members",
            owner,
            JsonContent.Create(new { email = Email(member), role })));
        add.EnsureSuccessStatusCode();

        return member with { TenantId = owner.TenantId, TenantSlug = owner.TenantSlug };
    }

    private async Task ExpireInvitationAsync(Guid invitationId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var past = DateTimeOffset.UtcNow.AddDays(-1);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE tenancy.tenant_invitations SET expires_at = {past} WHERE id = {invitationId}");
    }
}
