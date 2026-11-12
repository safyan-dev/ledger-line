using System.Globalization;
using System.Text.Json;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class JsonWebhookPayloadParser : IWebhookPayloadParser
{
    public Result<ParsedWebhookEvent> Parse(string provider, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return Result.Failure<ParsedWebhookEvent>(
                Error.Validation("webhook.malformed", "Webhook payload is empty."));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            return Result.Failure<ParsedWebhookEvent>(
                Error.Unprocessable("webhook.malformed", "Webhook payload is not valid JSON."));
        }

        using (document)
        {
            var root = document.RootElement;

            var providerEventId = GetString(root, "id");
            var eventType = GetString(root, "type");

            var failures = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(providerEventId))
            {
                failures["id"] = ["Provider event id is required."];
            }

            if (string.IsNullOrWhiteSpace(eventType))
            {
                failures["type"] = ["Event type is required."];
            }

            if (failures.Count > 0)
            {
                return Result.Failure<ParsedWebhookEvent>(
                    Error.Validation("webhook.malformed", "Webhook payload is missing required fields.", failures));
            }

            if (!string.Equals(eventType, "payment.succeeded", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(eventType, "payment.failed", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(new ParsedWebhookEvent(
                    providerEventId!,
                    eventType!,
                    Payment: null,
                    IgnoredReason: $"Event type '{eventType}' is not handled."));
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<ParsedWebhookEvent>(
                    Error.Validation("webhook.malformed", "Payment webhook is missing the data object."));
            }

            var providerPaymentId = GetString(data, "paymentId") ?? GetString(data, "payment_id");
            if (string.IsNullOrWhiteSpace(providerPaymentId))
            {
                failures["data.paymentId"] = ["Payment id is required."];
            }

            var reference = GetString(data, "reference");
            var customerReference = GetString(data, "customerReference") ?? GetString(data, "customer");

            var orderIdValue = GetString(data, "orderId") ?? GetString(data, "order_id");
            Guid? orderId = null;

            if (!string.IsNullOrWhiteSpace(orderIdValue))
            {
                if (Guid.TryParse(orderIdValue, out var parsedOrderId))
                {
                    orderId = parsedOrderId;
                }
                else
                {
                    failures["data.orderId"] = ["Order id must be a valid GUID."];
                }
            }

            if (orderId is null
                && string.IsNullOrWhiteSpace(reference)
                && string.IsNullOrWhiteSpace(customerReference))
            {
                failures["data.orderId"] = ["Order id, reference or customer reference is required."];
            }

            var amountMinor = GetDecimal(data, "amount");
            if (amountMinor is null or <= 0)
            {
                failures["data.amount"] = ["Amount must be greater than zero (minor units)."];
            }

            var currency = GetString(data, "currency");
            if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            {
                failures["data.currency"] = ["Currency must be a three-letter ISO code."];
            }

            if (failures.Count > 0)
            {
                return Result.Failure<ParsedWebhookEvent>(
                    Error.Validation("webhook.malformed", "Webhook payment data is invalid.", failures));
            }

            var amount = decimal.Round(amountMinor!.Value / 100m, 2, MidpointRounding.AwayFromZero);
            var receivedAt = GetDateTimeOffset(data, "receivedAt")
                             ?? GetUnixTimestamp(root, "created")
                             ?? DateTimeOffset.UtcNow;

            var payment = new ProviderPaymentData(
                provider,
                providerPaymentId!,
                orderId,
                amount,
                currency!.ToUpperInvariant(),
                GetString(data, "customerName"),
                customerReference,
                reference,
                receivedAt,
                GetString(data, "failureReason") ?? GetString(data, "failureMessage"));

            return Result.Success(new ParsedWebhookEvent(providerEventId!, eventType!, payment, IgnoredReason: null));
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
            JsonValueKind.String when decimal.TryParse(
                property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) => value,
            _ => null
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property)
           && property.ValueKind == JsonValueKind.String
           && DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : null;

    private static DateTimeOffset? GetUnixTimestamp(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(property.GetInt64())
            : null;
}
