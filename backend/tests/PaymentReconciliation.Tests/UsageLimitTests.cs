using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentReconciliation.Persistence;
using PaymentReconciliation.Tests.Infrastructure;

namespace PaymentReconciliation.Tests;

[Collection(LedgerlineCollection.Name)]
public sealed class UsageLimitTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Usage_reports_counts_limits_and_features()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("usagebase"));

        var usage = await GetUsageAsync(client, owner);
        Assert.Equal("free", usage.GetProperty("planCode").GetString());

        var orders = FindMetric(usage, "OrdersPerMonth");
        Assert.Equal(0, orders.GetProperty("used").GetInt32());
        Assert.Equal(500, orders.GetProperty("limit").GetInt32());

        var team = FindMetric(usage, "TeamMembers");
        Assert.Equal(1, team.GetProperty("used").GetInt32());
        Assert.Equal(2, team.GetProperty("limit").GetInt32());

        var apiAccess = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/usage/features/api_access",
            owner));
        apiAccess.EnsureSuccessStatusCode();
        var apiAccessBody = await apiAccess.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(apiAccessBody.GetProperty("enabled").GetBoolean());

        await client.CreateOrderAsync(owner);

        var after = await GetUsageAsync(client, owner);
        Assert.Equal(1, FindMetric(after, "OrdersPerMonth").GetProperty("used").GetInt32());
    }

    [Fact]
    public async Task Order_and_payment_limits_block_creation()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("usagelimits"));

        try
        {
            await SetPlanLimitAsync("free", "orders_per_month", 0);

            var order = await client.SendAsync(client.Authorized(
                HttpMethod.Post,
                "/api/orders",
                owner,
                JsonContent.Create(new
                {
                    customerName = "Blocked Order",
                    customerReference = $"BLOCK-{Guid.NewGuid():N}",
                    expectedAmount = 10,
                    currency = "USD"
                })));
            Assert.Equal(HttpStatusCode.Forbidden, order.StatusCode);

            await SetPlanLimitAsync("free", "payments_per_month", 0);

            var payment = await client.SendAsync(client.Authorized(
                HttpMethod.Post,
                "/api/payments",
                owner,
                JsonContent.Create(new
                {
                    amount = 10,
                    currency = "USD",
                    customerName = "Blocked Payment",
                    reference = $"PAY-{Guid.NewGuid():N}"
                })));
            Assert.Equal(HttpStatusCode.Forbidden, payment.StatusCode);
        }
        finally
        {
            await SetPlanLimitAsync("free", "orders_per_month", 500);
            await SetPlanLimitAsync("free", "payments_per_month", 500);
        }
    }

    [Fact]
    public async Task Team_limit_blocks_direct_add_and_invitation_acceptance()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("usageteam"));
        var finance = await client.RegisterTenantAsync(NewSlug("usageteamfin"));

        try
        {
            var added = await client.SendAsync(client.Authorized(
                HttpMethod.Post,
                "/api/tenants/current/members",
                owner,
                JsonContent.Create(new { email = Email(finance), role = "Finance" })));
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);

            var third = await client.RegisterTenantAsync(NewSlug("usageteamthird"));
            var thirdAdd = await client.SendAsync(client.Authorized(
                HttpMethod.Post,
                "/api/tenants/current/members",
                owner,
                JsonContent.Create(new { email = Email(third), role = "Finance" })));
            Assert.Equal(HttpStatusCode.Forbidden, thirdAdd.StatusCode);

            var inviteeEmail = $"limit-invite-{Guid.NewGuid():N}@example.test";
            var invitation = await client.SendAsync(client.Authorized(
                HttpMethod.Post,
                "/api/tenants/current/invitations",
                owner,
                JsonContent.Create(new { email = inviteeEmail, role = "Finance" })));
            Assert.Equal(HttpStatusCode.OK, invitation.StatusCode);

            var token = (await invitation.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("token")
                .GetString();

            var blocked = await client.PostAsJsonAsync("/api/invitations/accept", new
            {
                token,
                displayName = "Seat Blocked",
                password = "InvitePass123!"
            });
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

            var removed = await client.SendAsync(client.Authorized(
                HttpMethod.Delete,
                $"/api/tenants/current/members/{finance.UserId}",
                owner));
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

            var accepted = await client.PostAsJsonAsync("/api/invitations/accept", new
            {
                token,
                displayName = "Seat Available",
                password = "InvitePass123!"
            });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        finally
        {
            await SetPlanLimitAsync("free", "team_members", 2);
        }
    }

    [Fact]
    public async Task Concurrent_order_creation_cannot_exceed_the_limit()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("usageconcurrent"));

        try
        {
            await SetPlanLimitAsync("free", "orders_per_month", 1);

            var requests = Enumerable.Range(0, 10)
                .Select(index => client.SendAsync(client.Authorized(
                    HttpMethod.Post,
                    "/api/orders",
                    owner,
                    JsonContent.Create(new
                    {
                        customerName = $"Concurrent {index}",
                        customerReference = $"CC-{Guid.NewGuid():N}",
                        description = "concurrent",
                        expectedAmount = 10,
                        currency = "USD"
                    }))))
                .ToList();

            var responses = await Task.WhenAll(requests);
            var successes = responses.Count(response => response.StatusCode == HttpStatusCode.Created);
            var forbidden = responses.Count(response => response.StatusCode == HttpStatusCode.Forbidden);

            Assert.Equal(1, successes);
            Assert.Equal(9, forbidden);
        }
        finally
        {
            await SetPlanLimitAsync("free", "orders_per_month", 500);
        }
    }

    [Fact]
    public async Task Webhook_payment_over_limit_is_failed_and_can_be_reprocessed()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("usagewebhook"));
        var eventId = $"evt_{Guid.NewGuid():N}";
        var providerPaymentId = $"pay_{Guid.NewGuid():N}";

        try
        {
            await SetPlanLimitAsync("free", "payments_per_month", 0);
            await SendSignedWebhookAsync(client, owner, eventId, providerPaymentId);

            var failed = await WaitForWebhookAsync(
                client,
                owner,
                eventId,
                item => item.GetProperty("status").GetString() == "Failed");

            Assert.StartsWith("plan.limit_reached", failed.GetProperty("failureReason").GetString());

            await SetPlanLimitAsync("free", "payments_per_month", 5000);

            var webhookId = failed.GetProperty("id").GetGuid();
            var reprocess = await client.SendAsync(client.Authorized(
                HttpMethod.Post,
                $"/api/webhooks/{webhookId}/reprocess",
                owner));
            Assert.Equal(HttpStatusCode.NoContent, reprocess.StatusCode);

            var processed = await WaitForWebhookAsync(
                client,
                owner,
                eventId,
                item => item.GetProperty("status").GetString() == "Processed");

            Assert.NotEqual(JsonValueKind.Null, processed.GetProperty("paymentId").ValueKind);
        }
        finally
        {
            await SetPlanLimitAsync("free", "payments_per_month", 500);
        }
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

    private static string Email(TestSession session)
        => $"owner@{session.TenantSlug}.test";

    private static JsonElement FindMetric(JsonElement usage, string key)
        => usage.GetProperty("metrics")
            .EnumerateArray()
            .First(metric => metric.GetProperty("key").GetString() == key);

    private static async Task<JsonElement> GetUsageAsync(HttpClient client, TestSession session)
    {
        var response = await client.SendAsync(client.Authorized(HttpMethod.Get, "/api/usage", session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task SetPlanLimitAsync(string planCode, string key, int value)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE tenancy.plan_limits SET value = {value} WHERE key = {key} AND plan_id = (SELECT id FROM tenancy.plans WHERE code = {planCode})");
    }

    private static async Task SendSignedWebhookAsync(
        HttpClient client,
        TestSession session,
        string eventId,
        string providerPaymentId)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "payment.succeeded",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                paymentId = providerPaymentId,
                amount = 1000,
                currency = "USD",
                reference = $"WH-{Guid.NewGuid():N}",
                customerName = "Webhook Limit"
            }
        });

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("whsec_test_ledgerline"));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/mock")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Host = $"{session.TenantSlug}.localhost";
        request.Headers.Add("X-Provider-Signature", $"sha256={signature}");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static async Task<JsonElement> WaitForWebhookAsync(
        HttpClient client,
        TestSession session,
        string eventId,
        Func<JsonElement, bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var response = await client.SendAsync(client.Authorized(
                HttpMethod.Get,
                $"/api/webhooks?search={eventId}&pageSize=10",
                session));
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var item in body.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("providerEventId").GetString() == eventId && predicate(item))
                {
                    return item;
                }
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException($"Webhook event {eventId} did not reach the expected state.");
    }
}
