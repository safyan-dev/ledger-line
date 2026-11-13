namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record SubscriptionDto(
    Guid Id,
    string PlanCode,
    string PlanName,
    string Status,
    string ActivationSource,
    bool PaymentConfirmed,
    DateTimeOffset StartDate,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    string? PendingPlanCode,
    DateTimeOffset? PendingPlanEffectiveAt);

public sealed record ChangeSubscriptionPlanRequest(string PlanCode);

public sealed record RenewSubscriptionRequest(string IdempotencyKey);

public sealed record EntitlementsDto(
    string PlanCode,
    string PlanName,
    string Status,
    IReadOnlyList<PlanFeatureDto> Features,
    IReadOnlyList<PlanLimitDto> Limits);
