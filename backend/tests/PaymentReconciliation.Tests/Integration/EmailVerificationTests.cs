using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentReconciliation.Tests.Integration.Infrastructure;
using Xunit;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class EmailVerificationTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Tenant_registration_requires_email_confirmation_before_login()
    {
        var client = factory.CreateClient();
        var slug = NewSlug("verify");
        var email = $"owner@{slug}.test";

        var registration = await client.PostAsJsonAsync("/api/tenants/register", new
        {
            companyName = "Verification Company",
            tenantSlug = slug,
            ownerName = "Verification Owner",
            email,
            password = "TenantPass123!"
        });

        registration.EnsureSuccessStatusCode();
        var body = await registration.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("session").ValueKind);
        Assert.True(body.GetProperty("emailVerificationRequired").GetBoolean());
        Assert.False(body.TryGetProperty("emailVerificationToken", out _));
        var token = TestEmailSender.LatestToken(email, "Confirm your email");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var loginBefore = await client.LoginRequestAsync(email, "TenantPass123!", slug);
        Assert.Equal(HttpStatusCode.Forbidden, loginBefore.StatusCode);
        var loginBody = await loginBefore.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.email_not_confirmed", loginBody.GetProperty("code").GetString());

        var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email", new { email, token });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var loginAfter = await client.LoginRequestAsync(email, "TenantPass123!", slug);
        Assert.Equal(HttpStatusCode.OK, loginAfter.StatusCode);
        var loginAfterBody = await loginAfter.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(loginAfterBody.GetProperty("user").GetProperty("emailConfirmed").GetBoolean());
    }

    [Fact]
    public async Task Resend_confirmation_does_not_reveal_unknown_accounts()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new
        {
            email = $"unknown-{Guid.NewGuid():N}@example.test"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}
