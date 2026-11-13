using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Contracts.Events;

public sealed record SubscriptionPlanChanged(
    Guid TenantId,
    Guid SubscriptionId,
    Guid PlanId,
    bool IsDowngrade,
    DateTimeOffset ChangedAt) : DomainEvent;

public sealed record SubscriptionPlanChangeScheduled(
    Guid TenantId,
    Guid SubscriptionId,
    Guid PlanId,
    DateTimeOffset? EffectiveAt,
    DateTimeOffset ScheduledAt) : DomainEvent;

public sealed record SubscriptionCancellationScheduled(
    Guid TenantId,
    Guid SubscriptionId,
    DateTimeOffset? EffectiveAt,
    DateTimeOffset ScheduledAt) : DomainEvent;

public sealed record SubscriptionRenewed(
    Guid TenantId,
    Guid SubscriptionId,
    DateTimeOffset CurrentPeriodEnd,
    DateTimeOffset RenewedAt) : DomainEvent;

public sealed record SubscriptionExpired(
    Guid TenantId,
    Guid SubscriptionId,
    DateTimeOffset? ExpiredAt,
    DateTimeOffset DetectedAt) : DomainEvent;

public sealed record SubscriptionSuspended(
    Guid TenantId,
    Guid SubscriptionId,
    DateTimeOffset SuspendedAt) : DomainEvent;
