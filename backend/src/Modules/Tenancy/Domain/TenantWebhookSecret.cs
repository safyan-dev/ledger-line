using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Domain;

public sealed class TenantWebhookSecret : ITenantOwned
{
    private TenantWebhookSecret()
    {
    }

    private TenantWebhookSecret(
        Guid id,
        Guid tenantId,
        string provider,
        string protectedSecret,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Provider = provider;
        ProtectedSecret = protectedSecret;
        CreatedAt = createdAt;
        RotatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProtectedSecret { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset RotatedAt { get; private set; }

    public static TenantWebhookSecret Create(
        Guid tenantId,
        string provider,
        string protectedSecret,
        DateTimeOffset now)
        => new(Guid.NewGuid(), tenantId, provider, protectedSecret, now);

    public void Rotate(string protectedSecret, DateTimeOffset now)
    {
        ProtectedSecret = protectedSecret;
        RotatedAt = now;
    }
}
