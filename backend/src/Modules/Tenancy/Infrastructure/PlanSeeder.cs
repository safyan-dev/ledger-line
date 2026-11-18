using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Infrastructure;

public sealed class PlanSeeder(IAppDbContext dbContext, IUnitOfWork unitOfWork)
{
    private static readonly (string Key, string Name, string Description)[] FeatureDefinitions =
    [
        ("reconciliation.basic", "Basic reconciliation", "Reference and amount matching for everyday payment review."),
        ("reconciliation.advanced", "Advanced reconciliation", "Multi-strategy matching with mismatch classification."),
        ("csv_export", "CSV export", "Export records for Finance systems and spreadsheets."),
        ("priority_support", "Priority support", "Prioritised support for high-volume operations.")
    ];

    private static readonly string[] ObsoleteFeatureKeys = ["api_access", "custom_roles"];

    private static readonly PlanDefinition[] PlanDefinitions =
    [
        new(
            "free",
            "Free",
            "For a small finance team getting payments under control.",
            "Solo operators and small teams",
            0m,
            "Free",
            false,
            1,
            [
                ("reconciliation.basic", true, null),
                ("reconciliation.advanced", false, null),
                ("csv_export", false, null),
                ("priority_support", false, null)
            ],
            [
                ("team_members", "Team members", 2, "2 members"),
                ("orders_per_month", "Orders per month", 500, "500 / month"),
                ("payments_per_month", "Payments per month", 500, "500 / month"),
                ("audit_history_days", "Audit history", 7, "7 days")
            ]),
        new(
            "growth",
            "Growth",
            "For teams reconciling daily payment volume across providers.",
            "Growing finance and operations teams",
            null,
            "Contact us",
            true,
            2,
            [
                ("reconciliation.basic", false, "Included in advanced"),
                ("reconciliation.advanced", true, null),
                ("csv_export", true, null),
                ("priority_support", false, null)
            ],
            [
                ("team_members", "Team members", 5, "5 members"),
                ("orders_per_month", "Orders per month", 5_000, "5,000 / month"),
                ("payments_per_month", "Payments per month", 5_000, "5,000 / month"),
                ("audit_history_days", "Audit history", 90, "90 days")
            ]),
        new(
            "business",
            "Business",
            "For high-volume operations that need controls and support.",
            "High-volume finance organisations",
            null,
            "Contact us",
            false,
            3,
            [
                ("reconciliation.basic", false, "Included in advanced"),
                ("reconciliation.advanced", true, null),
                ("csv_export", true, null),
                ("priority_support", true, null)
            ],
            [
                ("team_members", "Team members", 15, "15 members"),
                ("orders_per_month", "Orders per month", 25_000, "25,000 / month"),
                ("payments_per_month", "Payments per month", 25_000, "25,000 / month"),
                ("audit_history_days", "Audit history", 365, "365 days")
            ])
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var features = await dbContext.Set<Feature>().ToListAsync(cancellationToken);
        var obsoleteFeatures = features
            .Where(feature => ObsoleteFeatureKeys.Contains(feature.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (obsoleteFeatures.Count > 0)
        {
            var obsoleteIds = obsoleteFeatures.Select(feature => feature.Id).ToList();
            var obsoletePlanFeatures = await dbContext.Set<PlanFeature>()
                .Where(planFeature => obsoleteIds.Contains(planFeature.FeatureId))
                .ToListAsync(cancellationToken);

            dbContext.Set<PlanFeature>().RemoveRange(obsoletePlanFeatures);
            dbContext.Set<Feature>().RemoveRange(obsoleteFeatures);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            features = features.Except(obsoleteFeatures).ToList();
        }

        var featuresByKey = features.ToDictionary(feature => feature.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, name, description) in FeatureDefinitions)
        {
            if (featuresByKey.ContainsKey(key))
            {
                continue;
            }

            var feature = Feature.Create(key, name, description);
            dbContext.Set<Feature>().Add(feature);
            featuresByKey[key] = feature;
        }

        var existingCodes = await dbContext.Set<Plan>()
            .Select(plan => plan.Code)
            .ToListAsync(cancellationToken);
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in PlanDefinitions)
        {
            if (existing.Contains(definition.Code))
            {
                continue;
            }

            var plan = Plan.Create(
                definition.Code,
                definition.Name,
                definition.Tagline,
                definition.Audience,
                definition.MonthlyPrice,
                definition.PriceNote,
                definition.IsRecommended,
                definition.SortOrder);

            dbContext.Set<Plan>().Add(plan);

            foreach (var (featureKey, isIncluded, note) in definition.Features)
            {
                dbContext.Set<PlanFeature>().Add(
                    PlanFeature.Create(plan.Id, featuresByKey[featureKey].Id, isIncluded, note));
            }

            foreach (var (limitKey, label, value, display) in definition.Limits)
            {
                dbContext.Set<PlanLimit>().Add(
                    PlanLimit.Create(plan.Id, limitKey, label, value, display));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private sealed record PlanDefinition(
        string Code,
        string Name,
        string Tagline,
        string Audience,
        decimal? MonthlyPrice,
        string PriceNote,
        bool IsRecommended,
        int SortOrder,
        (string Key, bool IsIncluded, string? Note)[] Features,
        (string Key, string Label, int? Value, string Display)[] Limits);
}
