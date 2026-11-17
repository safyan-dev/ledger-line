using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Auditing.Infrastructure;

public sealed class PlatformAuditLogWriter(
    IAppDbContext dbContext,
    ICurrentUser currentUser) : IPlatformAuditLogWriter
{
    public void Add(PlatformAuditRecord record)
    {
        var actor = currentUser.Actor with { Role = "PlatformAdmin" };

        dbContext.Set<PlatformAuditLog>().Add(PlatformAuditLog.Create(
            actor,
            currentUser.Email,
            record.Action,
            record.TargetType,
            record.TargetId,
            record.TenantId,
            record.Summary,
            record.Metadata));
    }
}
