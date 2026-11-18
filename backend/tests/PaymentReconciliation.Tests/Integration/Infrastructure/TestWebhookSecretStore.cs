using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Tests.Integration.Infrastructure;

public sealed class TestWebhookSecretStore : IWebhookSecretStore
{
    public Task<string?> GetSecretAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(WebhookTestClient.SigningSecret);

    public Task EnsureSecretsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<string> RotateSecretAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken = default)
        => Task.FromResult(WebhookTestClient.SigningSecret);
}
