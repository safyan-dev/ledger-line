using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PaymentReconciliation.Tests.Infrastructure;

public sealed record TestSession(string AccessToken, string RefreshToken, Guid TenantId, string TenantSlug, Guid UserId);

public static class TestClientExtensions
{
    public static async Task<TestSession> RegisterTenantAsync(
        this HttpClient client,
        string slug,
        string? planCode = null)
    {
        var response = await client.PostAsJsonAsync("/api/tenants/register", new
        {
            companyName = $"{slug} company",
            tenantSlug = slug,
            ownerName = $"{slug} owner",
            email = $"owner@{slug}.test",
            password = "TenantPass123!",
            planCode
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var session = body.GetProperty("session");
        var tenant = body.GetProperty("tenant");
        var accessToken = session.GetProperty("accessToken").GetString()!;

        return new TestSession(
            accessToken,
            session.GetProperty("refreshToken").GetString()!,
            tenant.GetProperty("id").GetGuid(),
            tenant.GetProperty("slug").GetString()!,
            await client.GetUserIdAsync(accessToken));
    }

    public static async Task<TestSession> LoginAsync(this HttpClient client, string email, Guid tenantId, string tenantSlug)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "TenantPass123!" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString()!;

        return new TestSession(
            accessToken,
            body.GetProperty("refreshToken").GetString()!,
            tenantId,
            tenantSlug,
            body.GetProperty("user").GetProperty("id").GetGuid());
    }

    private static async Task<Guid> GetUserIdAsync(this HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public static HttpRequestMessage Authorized(
        this HttpClient client,
        HttpMethod method,
        string path,
        TestSession session,
        HttpContent? content = null)
        => client.AuthorizedAs(method, path, session.AccessToken, session.TenantSlug, content);

    public static HttpRequestMessage AuthorizedAs(
        this HttpClient client,
        HttpMethod method,
        string path,
        string accessToken,
        string tenantSlug,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Host = $"{tenantSlug}.localhost";
        return request;
    }

    public static async Task<Guid> CreateOrderAsync(this HttpClient client, TestSession session, decimal amount = 100)
    {
        var response = await client.SendAsync(client.Authorized(HttpMethod.Post, "/api/orders", session,
            JsonContent.Create(new
            {
                customerName = "Tenant customer",
                customerReference = $"REF-{Guid.NewGuid():N}",
                description = "test order",
                expectedAmount = amount,
                currency = "USD",
                orderNumber = (string?)null
            })));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
