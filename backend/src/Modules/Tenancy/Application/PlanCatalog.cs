using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public interface IPlanCatalog
{
    Task<IReadOnlyList<PlanDto>> GetPlansAsync(CancellationToken cancellationToken = default);

    Task<Plan?> FindPlanAsync(string code, CancellationToken cancellationToken = default);

    Task<PlanDto?> FindPlanDtoAsync(string code, CancellationToken cancellationToken = default);
}

public sealed class PlanCatalog(IAppDbContext dbContext) : IPlanCatalog
{
    public async Task<IReadOnlyList<PlanDto>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await dbContext.Set<Plan>()
            .AsNoTracking()
            .Where(plan => plan.IsActive)
            .Include(plan => plan.Features)
            .ThenInclude(planFeature => planFeature.Feature)
            .Include(plan => plan.Limits)
            .OrderBy(plan => plan.SortOrder)
            .ToListAsync(cancellationToken);

        return plans.Select(ToDto).ToList();
    }

    public Task<Plan?> FindPlanAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToLowerInvariant();

        return dbContext.Set<Plan>()
            .FirstOrDefaultAsync(plan => plan.Code == normalized && plan.IsActive, cancellationToken);
    }

    public async Task<PlanDto?> FindPlanDtoAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToLowerInvariant();

        var plan = await dbContext.Set<Plan>()
            .AsNoTracking()
            .Include(candidate => candidate.Features)
            .ThenInclude(planFeature => planFeature.Feature)
            .Include(candidate => candidate.Limits)
            .FirstOrDefaultAsync(candidate => candidate.Code == normalized && candidate.IsActive, cancellationToken);

        return plan is null ? null : ToDto(plan);
    }

    private static PlanDto ToDto(Plan plan)
        => new(
            plan.Code,
            plan.Name,
            plan.Tagline,
            plan.Audience,
            plan.MonthlyPrice,
            plan.PriceNote,
            plan.IsRecommended,
            plan.SortOrder,
            PlanFormatting.OrderFeatures(plan.Features),
            PlanFormatting.OrderLimits(plan.Limits));
}
