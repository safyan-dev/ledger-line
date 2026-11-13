namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record PlanFeatureDto(string Key, string Label, bool Included, string? Note);

public sealed record PlanLimitDto(string Key, string Label, int? Value, string Display);

public sealed record PlanDto(
    string Code,
    string Name,
    string Tagline,
    string Audience,
    decimal? MonthlyPrice,
    string PriceNote,
    bool Recommended,
    int SortOrder,
    IReadOnlyList<PlanFeatureDto> Features,
    IReadOnlyList<PlanLimitDto> Limits);
