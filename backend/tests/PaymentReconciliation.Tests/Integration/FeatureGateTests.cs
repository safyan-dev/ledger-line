using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentReconciliation.Tests.Integration.Infrastructure;
using Xunit;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class FeatureGateTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Csv_export_is_gated_by_the_plan()
    {
        var client = factory.CreateClient();
        var free = await client.RegisterTenantAsync(NewSlug("featurecsvfree"));
        var growth = await client.RegisterTenantAsync(NewSlug("featurecsvgrowth"), "growth");

        var denied = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/reports/export/payments",
            free));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var deniedBody = await denied.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("plan.feature_not_included", deniedBody.GetProperty("code").GetString());

        var exported = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/reports/export/payments",
            growth));
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal("text/csv", exported.Content.Headers.ContentType?.MediaType);
        var csv = await exported.Content.ReadAsStringAsync();
        Assert.Contains("payment_id,provider,provider_payment_id", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task High_priority_support_is_gated_by_the_plan()
    {
        var client = factory.CreateClient();
        var free = await client.RegisterTenantAsync(NewSlug("featurepriorityfree"));
        var business = await client.RegisterTenantAsync(NewSlug("featureprioritybiz"), "business");

        var denied = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/support/tickets",
            free,
            JsonContent.Create(new
            {
                subject = "Priority request",
                description = "Please prioritise this.",
                category = "Other",
                priority = "High"
            })));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var deniedBody = await denied.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("plan.feature_not_included", deniedBody.GetProperty("code").GetString());

        var created = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            "/api/support/tickets",
            business,
            JsonContent.Create(new
            {
                subject = "Priority request",
                description = "Please prioritise this.",
                category = "Other",
                priority = "High"
            })));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}
