using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentReconciliation.Tests.Infrastructure;

namespace PaymentReconciliation.Tests;

[Collection(LedgerlineCollection.Name)]
public sealed class AuthTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Registered_owner_can_login_and_call_tenant_api()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"login-" + Guid.NewGuid().ToString("N")[..10]);

        var me = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/auth/me", session));
        var orders = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/orders", session));

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.OK, orders.StatusCode);
    }

    [Fact]
    public async Task Login_is_tenant_agnostic()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"agnostic-" + Guid.NewGuid().ToString("N")[..10]);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"owner@{session.TenantSlug}.test",
            password = "TenantPass123!"
        });

        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("tenant", out _));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());
        request.Headers.Host = $"{session.TenantSlug}.localhost";

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_old_token_is_rejected()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"refresh-" + Guid.NewGuid().ToString("N")[..10]);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        refresh.EnsureSuccessStatusCode();
        var rotated = await refresh.Content.ReadFromJsonAsync<JsonElement>();

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        var newToken = rotated.GetProperty("accessToken").GetString();

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
        request.Headers.Host = $"{session.TenantSlug}.localhost";
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"logout-" + Guid.NewGuid().ToString("N")[..10]);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = session.RefreshToken });
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Invalid_password_is_rejected()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"creds-" + Guid.NewGuid().ToString("N")[..10]);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"owner@{session.TenantSlug}.test",
            password = "WrongPass123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Changing_password_requires_current_password_and_revokes_sessions()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"changepw-" + Guid.NewGuid().ToString("N")[..10]);

        var wrong = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/auth/change-password",
            session,
            JsonContent.Create(new { currentPassword = "NotThePass1!", newPassword = "NewPass123!" })));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var changed = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/auth/change-password",
            session,
            JsonContent.Create(new { currentPassword = "TenantPass123!", newPassword = "NewPass123!" })));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_enumerate_accounts()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"forgot-" + Guid.NewGuid().ToString("N")[..10]);

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email = $"owner@{session.TenantSlug}.test"
        });
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email = $"nobody-{Guid.NewGuid():N}@example.test"
        });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
    }

    [Fact]
    public async Task Reset_password_token_works_once()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"reset-" + Guid.NewGuid().ToString("N")[..10]);
        var email = $"owner@{session.TenantSlug}.test";

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        forgot.EnsureSuccessStatusCode();
        var token = (await forgot.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resetToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            email,
            token,
            newPassword = "ResetPass123!"
        });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var reuse = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            email,
            token,
            newPassword = "AnotherPass123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "ResetPass123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Duplicate_tenant_slug_is_rejected()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"dup-" + Guid.NewGuid().ToString("N")[..10]);

        var response = await client.PostAsJsonAsync("/api/tenants/register", new
        {
            companyName = "Duplicate",
            tenantSlug = session.TenantSlug,
            ownerName = "Other",
            email = $"other-{Guid.NewGuid():N}@example.test",
            password = "TenantPass123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task One_user_can_belong_to_multiple_tenants_with_a_single_token()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync($"switcha-" + Guid.NewGuid().ToString("N")[..10]);
        var secondSlug = $"switchb-" + Guid.NewGuid().ToString("N")[..10];
        var second = await client.RegisterTenantAsync(secondSlug);

        await factory.AddMembershipAsync(second.TenantId, first.UserId);

        var onFirstTenant = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/orders", first));

        var secondHostRequest = client.Authorized(HttpMethod.Get, "/api/orders", first);
        secondHostRequest.Headers.Host = $"{second.TenantSlug}.localhost";
        var secondTenantOrders = await client.SendAsync(secondHostRequest);

        var mine = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/tenants/mine", first));
        var body = await mine.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, onFirstTenant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondTenantOrders.StatusCode);
        Assert.Equal(2, body.GetArrayLength());
    }
}
