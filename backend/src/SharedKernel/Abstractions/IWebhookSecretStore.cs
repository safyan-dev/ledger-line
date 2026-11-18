namespace PaymentReconciliation.SharedKernel.Abstractions;

public interface IWebhookSecretStore
{
    Task<string?> GetSecretAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken = default);

    Task EnsureSecretsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<string> RotateSecretAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken = default);
}
