using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Tenancy.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Events;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Auditing.Application.EventHandlers;

public sealed class SubscriptionPlanChangedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SubscriptionPlanChanged>
{
    public Task<Result> HandleAsync(SubscriptionPlanChanged domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "subscription.plan_changed",
            "tenancy",
            "Subscription",
            domainEvent.SubscriptionId,
            domainEvent.IsDowngrade
                ? "Subscription plan changed as a scheduled downgrade."
                : "Subscription plan changed.",
            new { domainEvent.PlanId },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SubscriptionPlanChangeScheduledAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SubscriptionPlanChangeScheduled>
{
    public Task<Result> HandleAsync(SubscriptionPlanChangeScheduled domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "subscription.downgrade_scheduled",
            "tenancy",
            "Subscription",
            domainEvent.SubscriptionId,
            "Subscription downgrade scheduled for the end of the current period.",
            new { domainEvent.PlanId, domainEvent.EffectiveAt },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SubscriptionCancellationScheduledAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SubscriptionCancellationScheduled>
{
    public Task<Result> HandleAsync(
        SubscriptionCancellationScheduled domainEvent,
        CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "subscription.cancel_scheduled",
            "tenancy",
            "Subscription",
            domainEvent.SubscriptionId,
            "Subscription cancellation scheduled for the end of the current period.",
            new { domainEvent.EffectiveAt },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SubscriptionRenewedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SubscriptionRenewed>
{
    public Task<Result> HandleAsync(SubscriptionRenewed domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "subscription.renewed",
            "tenancy",
            "Subscription",
            domainEvent.SubscriptionId,
            "Subscription renewed.",
            new { domainEvent.CurrentPeriodEnd },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SubscriptionExpiredAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SubscriptionExpired>
{
    public Task<Result> HandleAsync(SubscriptionExpired domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "subscription.expired",
            "tenancy",
            "Subscription",
            domainEvent.SubscriptionId,
            "Subscription expired. Tenant data was retained.",
            new { domainEvent.ExpiredAt },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SubscriptionSuspendedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SubscriptionSuspended>
{
    public Task<Result> HandleAsync(SubscriptionSuspended domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "subscription.suspended",
            "tenancy",
            "Subscription",
            domainEvent.SubscriptionId,
            "Subscription suspended.",
            null,
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SubscriptionReactivatedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SubscriptionReactivated>
{
    public Task<Result> HandleAsync(SubscriptionReactivated domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "subscription.reactivated",
            "tenancy",
            "Subscription",
            domainEvent.SubscriptionId,
            domainEvent.WasExpired
                ? "Subscription reactivated after expiry."
                : "Scheduled subscription cancellation cleared.",
            new { domainEvent.PlanId },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SubscriptionCanceledAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SubscriptionCanceled>
{
    public Task<Result> HandleAsync(SubscriptionCanceled domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            AuditActor.System,
            "subscription.canceled",
            "tenancy",
            "Subscription",
            domainEvent.SubscriptionId,
            "Subscription canceled. Tenant data was retained.",
            null,
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}
