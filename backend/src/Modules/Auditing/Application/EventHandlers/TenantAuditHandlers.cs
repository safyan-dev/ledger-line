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

public sealed class TenantSettingsUpdatedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantSettingsUpdated>
{
    public Task<Result> HandleAsync(TenantSettingsUpdated domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.settings_updated",
            "tenancy",
            "Tenant",
            domainEvent.TenantId,
            "Workspace settings updated.",
            new
            {
                domainEvent.BusinessContactEmail,
                domainEvent.DefaultCurrency,
                domainEvent.TimeZone
            },
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

public sealed class TenantInvitationCreatedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantInvitationCreated>
{
    public Task<Result> HandleAsync(TenantInvitationCreated domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.invitation_created",
            "tenancy",
            "TenantInvitation",
            domainEvent.InvitationId,
            $"Invitation created for {domainEvent.Email} with the {domainEvent.Role} role.",
            new { domainEvent.Email, domainEvent.Role },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class TenantInvitationResentAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantInvitationResent>
{
    public Task<Result> HandleAsync(TenantInvitationResent domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.invitation_resent",
            "tenancy",
            "TenantInvitation",
            domainEvent.InvitationId,
            $"Invitation resent to {domainEvent.Email}.",
            new { domainEvent.Email, domainEvent.ExpiresAt },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class TenantInvitationAcceptedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantInvitationAccepted>
{
    public Task<Result> HandleAsync(TenantInvitationAccepted domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.invitation_accepted",
            "tenancy",
            "TenantInvitation",
            domainEvent.InvitationId,
            $"Invitation accepted with the {domainEvent.Role} role.",
            new { domainEvent.UserId, domainEvent.Role },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class TenantInvitationRevokedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<TenantInvitationRevoked>
{
    public Task<Result> HandleAsync(TenantInvitationRevoked domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "tenant.invitation_revoked",
            "tenancy",
            "TenantInvitation",
            domainEvent.InvitationId,
            $"Invitation to {domainEvent.Email} revoked.",
            new { domainEvent.Email },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}
