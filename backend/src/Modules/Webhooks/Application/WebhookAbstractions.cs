using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Webhooks.Application;

public sealed record ParsedWebhookEvent(
    string ProviderEventId,
    string EventType,
    ProviderPaymentData? Payment,
    string? IgnoredReason);

public interface IWebhookPayloadParser
{
    Result<ParsedWebhookEvent> Parse(string provider, string rawBody);
}

public interface IWebhookSignatureVerifier
{
    Task<Result> VerifyAsync(
        string provider,
        string rawBody,
        string? signatureHeader,
        CancellationToken cancellationToken = default);
}

public interface IWebhookEventRepository
{
    Task<Domain.WebhookEvent?> FindByProviderEventIdAsync(
        string provider,
        string providerEventId,
        CancellationToken cancellationToken = default);

    Task<Domain.WebhookEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Domain.WebhookEvent webhookEvent);
}
