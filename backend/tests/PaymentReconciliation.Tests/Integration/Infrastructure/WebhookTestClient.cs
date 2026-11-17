using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentReconciliation.Tests.Integration.Infrastructure;

internal static class WebhookTestClient
{
    public const string Provider = "mock";
    public const string SigningSecret = "whsec_test_ledgerline";

    public static string NewEventId() => $"evt_{Guid.NewGuid():N}";

    public static string NewProviderPaymentId() => $"pay_{Guid.NewGuid():N}";

    public static string PaymentPayload(
        string eventId,
        string providerPaymentId,
        decimal amount,
        string currency = "USD",
        Guid? orderId = null,
        string? reference = null,
        string? customerReference = null,
        string? customerName = "Webhook customer",
        string type = "payment.succeeded",
        string? failureReason = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = eventId,
            ["type"] = type,
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["data"] = new Dictionary<string, object?>
            {
                ["paymentId"] = providerPaymentId,
                ["orderId"] = orderId?.ToString(),
                ["amount"] = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
                ["currency"] = currency,
                ["reference"] = reference,
                ["customerReference"] = customerReference,
                ["customerName"] = customerName,
                ["failureReason"] = failureReason
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    public static Task<HttpResponseMessage> SendSignedAsync(
        HttpClient client,
        string tenantSlug,
        string payload,
        bool omitSignature = false,
        string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/{Provider}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Host = $"{tenantSlug}.localhost";

        if (!omitSignature)
        {
            request.Headers.Add("X-Provider-Signature", signature ?? $"sha256={Sign(payload)}");
        }

        return client.SendAsync(request);
    }

    public static string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static async Task<JsonElement> WaitForEventAsync(
        HttpClient client,
        TestSession session,
        string expectedEventId,
        Func<JsonElement, bool>? predicate = null)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var response = await client.SendAsync(client.Authorized(
                HttpMethod.Get,
                $"/api/webhooks?search={Uri.EscapeDataString(expectedEventId)}&pageSize=10",
                session));
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var item in body.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("providerEventId").GetString() == expectedEventId
                    && (predicate is null || predicate(item)))
                {
                    return item;
                }
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"Webhook event {expectedEventId} did not reach the expected state within 10 seconds.");
    }

    public static async Task<JsonElement> GetPaymentAsync(
        HttpClient client,
        TestSession session,
        Guid paymentId)
    {
        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            $"/api/payments/{paymentId}",
            session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> GetOrderAsync(
        HttpClient client,
        TestSession session,
        Guid orderId)
    {
        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            $"/api/orders/{orderId}",
            session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> GetPaymentsAsync(
        HttpClient client,
        TestSession session,
        Guid? orderId = null,
        string? search = null)
    {
        var query = orderId is { } id
            ? $"?orderId={id}"
            : search is not null
                ? $"?search={Uri.EscapeDataString(search)}"
                : string.Empty;

        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            $"/api/payments{query}",
            session));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<IReadOnlyList<JsonElement>> GetReconciliationAsync(
        HttpClient client,
        TestSession session,
        Guid paymentId)
    {
        var response = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            $"/api/reconciliation/payment/{paymentId}",
            session));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.EnumerateArray().ToList();
    }
}
