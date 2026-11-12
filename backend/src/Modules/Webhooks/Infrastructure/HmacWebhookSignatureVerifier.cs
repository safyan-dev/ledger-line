using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class HmacWebhookSignatureVerifier(IConfiguration configuration) : IWebhookSignatureVerifier
{
    public Result Verify(string provider, string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Result.Failure(Error.Unauthorized(
                "webhook.signature_missing",
                "Missing X-Provider-Signature header."));
        }

        var secret = configuration[$"Webhooks:Providers:{provider}:SigningSecret"]
                     ?? configuration["Webhooks:SigningSecret"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            return Result.Failure(Error.Unexpected(
                "webhook.signing_secret_missing",
                $"No signing secret is configured for provider '{provider}'."));
        }

        var provided = signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signatureHeader["sha256=".Length..]
            : signatureHeader;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();

        var providedBytes = Encoding.UTF8.GetBytes(provided.Trim().ToLowerInvariant());
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes)
            ? Result.Success()
            : Result.Failure(Error.Unauthorized("webhook.signature_invalid", "Webhook signature is invalid."));
    }
}
