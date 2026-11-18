using System.Security.Cryptography;
using System.Text;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class HmacWebhookSignatureVerifier(
    IWebhookSecretStore secretStore,
    ITenantContext tenantContext) : IWebhookSignatureVerifier
{
    public async Task<Result> VerifyAsync(
        string provider,
        string rawBody,
        string? signatureHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Result.Failure(Error.Unauthorized(
                "webhook.signature_missing",
                "Missing X-Provider-Signature header."));
        }

        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure(Error.Unauthorized(
                "webhook.tenant_required",
                "A tenant is required to verify webhook signatures."));
        }

        var secret = await secretStore.GetSecretAsync(tenantId, provider, cancellationToken);
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
