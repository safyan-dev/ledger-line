using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using PaymentReconciliation.Tests.Integration.Infrastructure;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class OrderWorkflowTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Create_order_persists_and_is_tenant_scoped()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("orderflowA"));
        var second = await client.RegisterTenantAsync(NewSlug("orderflowB"));
        var customerReference = $"FLOW-{Guid.NewGuid():N}";

        var create = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/orders",
            first,
            JsonContent.Create(new
            {
                customerName = "Flow Customer",
                customerReference,
                description = "Order workflow test",
                expectedAmount = 125.50m,
                currency = "USD",
                orderNumber = (string?)null
            })));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("id").GetGuid();

        Assert.Equal("Flow Customer", created.GetProperty("customerName").GetString());
        Assert.Equal(customerReference, created.GetProperty("customerReference").GetString());
        Assert.Equal(125.50m, created.GetProperty("expectedAmount").GetDecimal());
        Assert.Equal(0m, created.GetProperty("paidAmount").GetDecimal());
        Assert.Equal("Pending", created.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("orderNumber").GetString()));

        var read = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            $"/api/orders/{orderId}",
            first));
        read.EnsureSuccessStatusCode();
        var persisted = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, persisted.GetProperty("id").GetGuid());
        Assert.Equal(customerReference, persisted.GetProperty("customerReference").GetString());

        var firstList = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/orders?pageSize=25",
            first));
        firstList.EnsureSuccessStatusCode();
        var firstBody = await firstList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, firstBody.GetProperty("totalCount").GetInt32());
        Assert.Equal(orderId, firstBody.GetProperty("items")[0].GetProperty("id").GetGuid());

        var secondList = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/orders?pageSize=25",
            second));
        secondList.EnsureSuccessStatusCode();
        var secondBody = await secondList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, secondBody.GetProperty("totalCount").GetInt32());
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}
