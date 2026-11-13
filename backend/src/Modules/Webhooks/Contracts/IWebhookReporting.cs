namespace PaymentReconciliation.Modules.Webhooks.Contracts;

public interface IWebhookReporting
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
