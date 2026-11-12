using PaymentReconciliation.Modules.Auditing.Domain;

namespace PaymentReconciliation.Modules.Auditing.Application;

public interface IAuditLogRepository
{
    void Add(AuditLog auditLog);
}
