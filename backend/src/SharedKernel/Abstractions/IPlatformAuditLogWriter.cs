namespace PaymentReconciliation.SharedKernel.Abstractions;

public sealed record PlatformAuditRecord(
    string Action,
    string TargetType,
    Guid? TargetId,
    Guid? TenantId,
    string Summary,
    object? Metadata = null);

public interface IPlatformAuditLogWriter
{
    void Add(PlatformAuditRecord record);
}
