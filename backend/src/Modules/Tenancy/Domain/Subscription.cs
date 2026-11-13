using PaymentReconciliation.Modules.Tenancy.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Domain;

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Canceled,
    Expired,
    Suspended
}

public enum SubscriptionActivation
{
    Free,
    Simulated,
    Manual
}

public sealed class Subscription : AggregateRoot, ITenantOwned
{
    private static readonly TimeSpan BillingPeriod = TimeSpan.FromDays(30);

    private Subscription()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid PlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public SubscriptionActivation ActivationSource { get; private set; }

    public bool PaymentConfirmed { get; private set; }

    public DateTimeOffset StartDate { get; private set; }

    public DateTimeOffset CurrentPeriodStart { get; private set; }

    public DateTimeOffset? CurrentPeriodEnd { get; private set; }

    public bool CancelAtPeriodEnd { get; private set; }

    public Guid? PendingPlanId { get; private set; }

    public DateTimeOffset? PendingPlanEffectiveAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing;

    public static Subscription Provision(
        Guid tenantId,
        Guid planId,
        bool isFree,
        SubscriptionActivation activation,
        DateTimeOffset now)
    {
        return new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            ActivationSource = activation,
            PaymentConfirmed = isFree,
            StartDate = now,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = isFree ? null : now.Add(BillingPeriod),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void ChangePlanImmediate(
        Guid planId,
        bool isFree,
        SubscriptionActivation activation,
        bool paymentConfirmed,
        DateTimeOffset now)
    {
        PlanId = planId;
        ActivationSource = activation;
        PaymentConfirmed = paymentConfirmed;
        CurrentPeriodStart = now;
        CurrentPeriodEnd = isFree ? null : now.Add(BillingPeriod);
        CancelAtPeriodEnd = false;
        PendingPlanId = null;
        PendingPlanEffectiveAt = null;
        UpdatedAt = now;
        Raise(new SubscriptionPlanChanged(TenantId, Id, planId, false, now));
    }

    public void SchedulePlanChange(Guid planId, DateTimeOffset now)
    {
        PendingPlanId = planId;
        PendingPlanEffectiveAt = CurrentPeriodEnd;
        UpdatedAt = now;
        Raise(new SubscriptionPlanChangeScheduled(TenantId, Id, planId, CurrentPeriodEnd, now));
    }

    public void CancelAtPeriodEndNow(DateTimeOffset now)
    {
        CancelAtPeriodEnd = true;
        UpdatedAt = now;
        Raise(new SubscriptionCancellationScheduled(TenantId, Id, CurrentPeriodEnd, now));
    }

    public bool Renew(DateTimeOffset now, Guid? idempotentPlanId = null)
    {
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired or SubscriptionStatus.Suspended)
        {
            return false;
        }

        if (PendingPlanId is { } pendingPlanId)
        {
            PlanId = pendingPlanId;
            PendingPlanId = null;
            PendingPlanEffectiveAt = null;
        }
        else if (idempotentPlanId is { } planId)
        {
            PlanId = planId;
        }

        var periodStart = CurrentPeriodEnd is { } end && end > now ? end : now;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodStart.Add(BillingPeriod);
        StartDate = StartDate == default ? now : StartDate;
        CancelAtPeriodEnd = false;
        UpdatedAt = now;
        Raise(new SubscriptionRenewed(TenantId, Id, CurrentPeriodEnd.Value, now));
        return true;
    }

    public bool Expire(DateTimeOffset now)
    {
        if (!IsActive || CurrentPeriodEnd is not { } end || end > now)
        {
            return false;
        }

        Status = SubscriptionStatus.Expired;
        UpdatedAt = now;
        Raise(new SubscriptionExpired(TenantId, Id, end, now));
        return true;
    }

    public void Suspend(DateTimeOffset now)
    {
        if (Status == SubscriptionStatus.Suspended)
        {
            return;
        }

        Status = SubscriptionStatus.Suspended;
        UpdatedAt = now;
        Raise(new SubscriptionSuspended(TenantId, Id, now));
    }

    public void Activate(DateTimeOffset now)
    {
        if (Status == SubscriptionStatus.Active)
        {
            return;
        }

        Status = SubscriptionStatus.Active;
        UpdatedAt = now;
    }
}

public sealed class SubscriptionHistory : ITenantOwned
{
    private SubscriptionHistory()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SubscriptionId { get; private set; }

    public Guid? PreviousPlanId { get; private set; }

    public Guid PlanId { get; private set; }

    public string Event { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static SubscriptionHistory Create(
        Guid tenantId,
        Guid subscriptionId,
        Guid planId,
        string action,
        SubscriptionStatus status,
        Guid? previousPlanId = null,
        string? note = null,
        string? idempotencyKey = null,
        DateTimeOffset? occurredAt = null)
    {
        return new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            PlanId = planId,
            PreviousPlanId = previousPlanId,
            Event = action,
            Status = status.ToString(),
            Note = note,
            IdempotencyKey = idempotencyKey,
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow
        };
    }
}
