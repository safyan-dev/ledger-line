namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record UsageMetricDto(
    string Key,
    string Label,
    int Used,
    int? Limit,
    int? Remaining,
    string Period);

public sealed record UsageSummaryDto(
    string PlanCode,
    string PlanName,
    string Status,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    IReadOnlyList<UsageMetricDto> Metrics,
    IReadOnlyList<PlanFeatureDto> Features);

public sealed record FeatureAvailabilityDto(string Key, bool Enabled);
