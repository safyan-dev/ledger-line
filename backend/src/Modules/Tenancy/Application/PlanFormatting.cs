using PaymentReconciliation.Modules.Tenancy.Domain;

namespace PaymentReconciliation.Modules.Tenancy.Application;

internal static class PlanFormatting
{
    private static readonly string[] FeatureOrder =
    [
        "reconciliation.basic",
        "reconciliation.advanced",
        "api_access",
        "csv_export",
        "custom_roles",
        "priority_support"
    ];

    private static readonly string[] LimitOrder =
    [
        "team_members",
        "orders_per_month",
        "payments_per_month",
        "audit_history_days"
    ];

    public static IReadOnlyList<PlanFeatureDto> OrderFeatures(IEnumerable<PlanFeature> features)
        => features
            .OrderBy(feature => IndexOf(FeatureOrder, feature.Feature?.Key))
            .Select(feature => new PlanFeatureDto(
                feature.Feature!.Key,
                feature.Feature.Name,
                feature.IsIncluded,
                feature.Note))
            .ToList();

    public static IReadOnlyList<PlanLimitDto> OrderLimits(IEnumerable<PlanLimit> limits)
        => limits
            .OrderBy(limit => IndexOf(LimitOrder, limit.Key))
            .Select(limit => new PlanLimitDto(limit.Key, limit.Label, limit.Value, limit.Display))
            .ToList();

    private static int IndexOf(string[] order, string? key)
    {
        var index = key is null ? -1 : Array.IndexOf(order, key);
        return index < 0 ? int.MaxValue : index;
    }
}
