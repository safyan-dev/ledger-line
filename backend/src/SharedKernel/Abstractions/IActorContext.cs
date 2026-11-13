namespace PaymentReconciliation.SharedKernel.Abstractions;

public interface IActorContext
{
    Guid? TenantId { get; }

    string? Role { get; }

    void Set(Guid? tenantId, string? role);
}

public sealed class ActorContext : IActorContext
{
    public Guid? TenantId { get; private set; }

    public string? Role { get; private set; }

    public void Set(Guid? tenantId, string? role)
    {
        TenantId = tenantId;
        Role = role;
    }
}
