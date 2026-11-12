using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentReconciliation.Modules.Webhooks.Application;

public static class WebhookEnvelopeReader
{
    public static string ComputeHash(string rawBody)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();

    public static (string? ProviderEventId, string? EventType) ReadIdentifiers(string rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            var providerEventId = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;

            var eventType = root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
                ? type.GetString()
                : null;

            return (providerEventId, eventType);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
