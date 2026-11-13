using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts.Events;
using PaymentReconciliation.SharedKernel.Events;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Auditing.Application.EventHandlers;

public sealed class TenantRenamedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantRenamed>
{
    public Task<Result> HandleAsync(TenantRenamed domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.renamed",
            "tenancy",
            "Tenant",
            domainEvent.TenantId,
            $"Tenant renamed to '{domainEvent.Name}'.",
            new { domainEvent.Name },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class TenantMemberAddedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantMemberAdded>
{
    public Task<Result> HandleAsync(TenantMemberAdded domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.member_added",
            "tenancy",
            "TenantUser",
            domainEvent.UserId,
            $"Team member added with the {domainEvent.Role} role.",
            new { domainEvent.UserId, domainEvent.Role },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class TenantMemberRoleChangedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantMemberRoleChanged>
{
    public Task<Result> HandleAsync(TenantMemberRoleChanged domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.member_role_changed",
            "tenancy",
            "TenantUser",
            domainEvent.UserId,
            $"Team member role changed from {domainEvent.PreviousRole} to {domainEvent.NewRole}.",
            new { domainEvent.PreviousRole, domainEvent.NewRole },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class TenantMemberRemovedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantMemberRemoved>
{
    public Task<Result> HandleAsync(TenantMemberRemoved domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.member_removed",
            "tenancy",
            "TenantUser",
            domainEvent.UserId,
            $"Team member with the {domainEvent.Role} role removed.",
            new { domainEvent.UserId, domainEvent.Role },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}
