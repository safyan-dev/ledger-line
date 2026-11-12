using PaymentReconciliation.Modules.Auditing.Application;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Auditing.Infrastructure;

public sealed class AuditLogRepository(IAppDbContext dbContext) : IAuditLogRepository
{
    public void Add(AuditLog auditLog)
        => dbContext.Set<AuditLog>().Add(auditLog);
}
