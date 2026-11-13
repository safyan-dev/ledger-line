using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentReconciliation.Tests.Infrastructure;

namespace PaymentReconciliation.Tests;

[Collection(LedgerlineCollection.Name)]
public sealed class TenantIsolationTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Tenant_apis_require_authentication()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_a_cannot_read_tenant_b_orders_or_payments()
    {
        var client = factory.CreateClient();
        var a = await client.RegisterTenantAsync($"isola-" + Guid.NewGuid().ToString("N")[..10]);
        var b = await client.RegisterTenantAsync($"isolb-" + Guid.NewGuid().ToString("N")[..10]);
        var orderId = await client.CreateOrderAsync(a);

        var orderResponse = await client.SendAsync(client.Authorized(HttpMethod.Get, $"/api/orders/{orderId}", b));
        var paymentResponse = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/payments", b));
        var auditResponse = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/audit-logs", b));
        var reconciliationResponse = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/reconciliation", b));

        Assert.Equal(HttpStatusCode.NotFound, orderResponse.StatusCode);
        Assert.Equal(0, (await paymentResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await auditResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await reconciliationResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Tenant_a_cannot_update_or_delete_tenant_b_orders()
    {
        var client = factory.CreateClient();
        var a = await client.RegisterTenantAsync($"updatea-" + Guid.NewGuid().ToString("N")[..10]);
        var b = await client.RegisterTenantAsync($"updateb-" + Guid.NewGuid().ToString("N")[..10]);
        var orderId = await client.CreateOrderAsync(b);

        var cancel = await client.SendAsync(client.Authorized(HttpMethod.Post, $"/api/orders/{orderId}/cancel", a,
            JsonContent.Create(new { reason = "cross-tenant attempt" })));

        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
    }

    [Fact]
    public async Task Non_member_is_rejected_on_another_subdomain()
    {
        var client = factory.CreateClient();
        var a = await client.RegisterTenantAsync($"hosta-" + Guid.NewGuid().ToString("N")[..10]);
        var b = await client.RegisterTenantAsync($"hostb-" + Guid.NewGuid().ToString("N")[..10]);

        var request = client.Authorized(HttpMethod.Get, "/api/orders", a);
        request.Headers.Host = $"{b.TenantSlug}.localhost";

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Forged_tenant_id_in_query_does_not_change_context()
    {
        var client = factory.CreateClient();
        var a = await client.RegisterTenantAsync($"forga-" + Guid.NewGuid().ToString("N")[..10]);
        var b = await client.RegisterTenantAsync($"forgb-" + Guid.NewGuid().ToString("N")[..10]);
        await client.CreateOrderAsync(b);
        await client.CreateOrderAsync(a);

        var response = await client.SendAsync(
            client.Authorized(HttpMethod.Get, $"/api/orders?tenantId={b.TenantId}", a));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Unknown_tenant_subdomain_returns_not_found()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync($"unknown-" + Guid.NewGuid().ToString("N")[..10]);
        var request = client.Authorized(HttpMethod.Get, "/api/orders", session);
        request.Headers.Host = "does-not-exist.localhost";

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_member_token_cannot_access_tenant()
    {
        var client = factory.CreateClient();
        var a = await client.RegisterTenantAsync($"membera-" + Guid.NewGuid().ToString("N")[..10]);
        var b = await client.RegisterTenantAsync($"memberb-" + Guid.NewGuid().ToString("N")[..10]);

        var request = client.Authorized(HttpMethod.Get, "/api/orders", a);
        request.Headers.Host = $"{b.TenantSlug}.localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
