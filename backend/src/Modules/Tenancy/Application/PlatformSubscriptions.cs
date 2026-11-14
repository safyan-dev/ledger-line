using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record PlatformSubscriptionDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string PlanCode,
    string PlanName,
    string Status,
    string ActivationSource,
    bool PaymentConfirmed,
    DateTimeOffset StartDate,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset? CurrentPeriodEnd,
    DateTimeOffset CreatedAt);

public sealed record GetPlatformSubscriptionsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null) : IQuery<PagedResult<PlatformSubscriptionDto>>;

public sealed class GetPlatformSubscriptionsHandler(IAppDbContext dbContext)
    : IQueryHandler<GetPlatformSubscriptionsQuery, PagedResult<PlatformSubscriptionDto>>
{
    public async Task<Result<PagedResult<PlatformSubscriptionDto>>> Handle(
        GetPlatformSubscriptionsQuery query,
        CancellationToken cancellationToken)
    {
        var source =
            from subscription in dbContext.Set<Subscription>().IgnoreQueryFilters().AsNoTracking()
            join tenant in dbContext.Set<Tenant>().AsNoTracking()
                on subscription.TenantId equals tenant.Id
            join plan in dbContext.Set<Plan>().AsNoTracking()
                on subscription.PlanId equals plan.Id
            select new { subscription, tenant.Name, plan.Code, PlanName = plan.Name };

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<SubscriptionStatus>(query.Status, true, out var status)
                || !Enum.IsDefined(status))
            {
                return Result.Failure<PagedResult<PlatformSubscriptionDto>>(Error.Validation(
                    "subscription.invalid_status",
                    "Subscription status is invalid."));
            }

            source = source.Where(row => row.subscription.Status == status);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await source.CountAsync(cancellationToken);
        var rows = await source
            .OrderByDescending(row => row.subscription.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new PlatformSubscriptionDto(
                row.subscription.Id,
                row.subscription.TenantId,
                row.Name,
                row.Code,
                row.PlanName,
                row.subscription.Status.ToString(),
                row.subscription.ActivationSource.ToString(),
                row.subscription.PaymentConfirmed,
                row.subscription.StartDate,
                row.subscription.CurrentPeriodStart,
                row.subscription.CurrentPeriodEnd,
                row.subscription.CreatedAt))
            .ToListAsync(cancellationToken);
        return Result.Success(PagedResult<PlatformSubscriptionDto>.Create(
            rows,
            page,
            pageSize,
            totalCount));
    }
}

public sealed record GetPlatformSubscriptionQuery(Guid SubscriptionId) : IQuery<PlatformSubscriptionDto>;

public sealed class GetPlatformSubscriptionHandler(IAppDbContext dbContext)
    : IQueryHandler<GetPlatformSubscriptionQuery, PlatformSubscriptionDto>
{
    public async Task<Result<PlatformSubscriptionDto>> Handle(
        GetPlatformSubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        var subscription = dbContext.Set<Subscription>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.SubscriptionId);
        var row = await (
            from candidate in subscription
            join tenant in dbContext.Set<Tenant>().AsNoTracking()
                on candidate.TenantId equals tenant.Id
            join plan in dbContext.Set<Plan>().AsNoTracking()
                on candidate.PlanId equals plan.Id
            select new PlatformSubscriptionDto(
                candidate.Id,
                candidate.TenantId,
                tenant.Name,
                plan.Code,
                plan.Name,
                candidate.Status.ToString(),
                candidate.ActivationSource.ToString(),
                candidate.PaymentConfirmed,
                candidate.StartDate,
                candidate.CurrentPeriodStart,
                candidate.CurrentPeriodEnd,
                candidate.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? Result.Failure<PlatformSubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "The subscription was not found."))
            : Result.Success(row);
    }
}

public sealed record ActivatePlatformSubscriptionCommand(Guid SubscriptionId)
    : ICommand<PlatformSubscriptionDto>;

public sealed class ActivatePlatformSubscriptionHandler(IAppDbContext dbContext)
    : ICommandHandler<ActivatePlatformSubscriptionCommand, PlatformSubscriptionDto>
{
    public async Task<Result<PlatformSubscriptionDto>> Handle(
        ActivatePlatformSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var subscription = await PlatformSubscriptionRows.FindSubscription(dbContext, command.SubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "The subscription was not found."));
        }

        var plan = await dbContext.Set<Plan>()
            .FirstAsync(candidate => candidate.Id == subscription.PlanId, cancellationToken);
        if (subscription.Status != SubscriptionStatus.Active)
        {
            var now = DateTimeOffset.UtcNow;
            subscription.ChangePlanImmediate(
                plan.Id,
                plan.IsFree,
                SubscriptionActivation.Manual,
                paymentConfirmed: false,
                now);
            dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
                subscription.TenantId,
                subscription.Id,
                plan.Id,
                "subscription.manually_activated",
                subscription.Status,
                note: "Manually activated by a platform administrator; no payment was processed.",
                occurredAt: now));
        }

        return await PlatformSubscriptionRows.GetResult(dbContext, subscription.Id, cancellationToken);
    }
}

public sealed record ChangePlatformSubscriptionPlanCommand(Guid SubscriptionId, string PlanCode)
    : ICommand<PlatformSubscriptionDto>;

public sealed class ChangePlatformSubscriptionPlanHandler(IAppDbContext dbContext)
    : ICommandHandler<ChangePlatformSubscriptionPlanCommand, PlatformSubscriptionDto>
{
    public async Task<Result<PlatformSubscriptionDto>> Handle(
        ChangePlatformSubscriptionPlanCommand command,
        CancellationToken cancellationToken)
    {
        var subscription = await PlatformSubscriptionRows.FindSubscription(dbContext, command.SubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "The subscription was not found."));
        }

        var targetPlan = await dbContext.Set<Plan>()
            .FirstOrDefaultAsync(
                plan => plan.Code == command.PlanCode.ToLowerInvariant() && plan.IsActive,
                cancellationToken);
        if (targetPlan is null)
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.Validation(
                "plan.not_found",
                "The selected plan is unavailable."));
        }

        var previousPlan = await dbContext.Set<Plan>()
            .FirstAsync(plan => plan.Id == subscription.PlanId, cancellationToken);
        if (previousPlan.Id == targetPlan.Id)
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.Conflict(
                "subscription.same_plan",
                "This tenant is already on that plan."));
        }

        var now = DateTimeOffset.UtcNow;
        subscription.ChangePlanImmediate(
            targetPlan.Id,
            targetPlan.IsFree,
            SubscriptionActivation.Manual,
            paymentConfirmed: false,
            now);
        dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
            subscription.TenantId,
            subscription.Id,
            targetPlan.Id,
            "subscription.manually_changed_plan",
            subscription.Status,
            previousPlan.Id,
            $"Plan changed manually from {previousPlan.Name} to {targetPlan.Name}; no payment was processed.",
            occurredAt: now));

        return await PlatformSubscriptionRows.GetResult(dbContext, subscription.Id, cancellationToken);
    }
}

public sealed record ExtendPlatformSubscriptionCommand(Guid SubscriptionId, int Days)
    : ICommand<PlatformSubscriptionDto>;

public sealed class ExtendPlatformSubscriptionHandler(IAppDbContext dbContext)
    : ICommandHandler<ExtendPlatformSubscriptionCommand, PlatformSubscriptionDto>
{
    public async Task<Result<PlatformSubscriptionDto>> Handle(
        ExtendPlatformSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var subscription = await PlatformSubscriptionRows.FindSubscription(dbContext, command.SubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "The subscription was not found."));
        }

        var now = DateTimeOffset.UtcNow;
        if (!subscription.ExtendPeriod(command.Days, now))
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.Validation(
                "subscription.invalid_extension",
                "The extension must be 1–365 days and the subscription cannot be canceled, expired or suspended."));
        }

        dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
            subscription.TenantId,
            subscription.Id,
            subscription.PlanId,
            "subscription.period_extended",
            subscription.Status,
            note: $"Current period extended by {command.Days} days by a platform administrator.",
            occurredAt: now));

        return await PlatformSubscriptionRows.GetResult(dbContext, subscription.Id, cancellationToken);
    }
}

public sealed record ChangePlatformSubscriptionStatusCommand(Guid SubscriptionId, string Status)
    : ICommand<PlatformSubscriptionDto>;

public sealed class ChangePlatformSubscriptionStatusHandler(IAppDbContext dbContext)
    : ICommandHandler<ChangePlatformSubscriptionStatusCommand, PlatformSubscriptionDto>
{
    public async Task<Result<PlatformSubscriptionDto>> Handle(
        ChangePlatformSubscriptionStatusCommand command,
        CancellationToken cancellationToken)
    {
        var subscription = await PlatformSubscriptionRows.FindSubscription(dbContext, command.SubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "The subscription was not found."));
        }

        var now = DateTimeOffset.UtcNow;
        if (string.Equals(command.Status, "Suspended", StringComparison.OrdinalIgnoreCase))
        {
            subscription.Suspend(now);
        }
        else if (string.Equals(command.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var plan = await dbContext.Set<Plan>()
                .FirstAsync(candidate => candidate.Id == subscription.PlanId, cancellationToken);
            if (subscription.Status != SubscriptionStatus.Active
                && !subscription.Reactivate(now, plan.IsFree))
            {
                return Result.Failure<PlatformSubscriptionDto>(Error.Conflict(
                    "subscription.cannot_reactivate",
                    "Use manual activation to activate a past-due subscription."));
            }
        }
        else
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.Validation(
                "subscription.invalid_status",
                "Status must be Active or Suspended."));
        }

        return await PlatformSubscriptionRows.GetResult(dbContext, subscription.Id, cancellationToken);
    }
}

internal static class PlatformSubscriptionRows
{
    public static async Task<Subscription?> FindSubscription(
        IAppDbContext dbContext,
        Guid subscriptionId,
        CancellationToken cancellationToken)
        => await dbContext.Set<Subscription>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                subscription => subscription.Id == subscriptionId,
                cancellationToken);

    public static async Task<Result<PlatformSubscriptionDto>> GetResult(
        IAppDbContext dbContext,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscription = await FindSubscription(dbContext, subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<PlatformSubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "The subscription was not found."));
        }

        var tenantName = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .Where(tenant => tenant.Id == subscription.TenantId)
            .Select(tenant => tenant.Name)
            .FirstAsync(cancellationToken);
        var plan = await dbContext.Set<Plan>()
            .AsNoTracking()
            .Where(candidate => candidate.Id == subscription.PlanId)
            .Select(candidate => new { candidate.Code, candidate.Name })
            .FirstAsync(cancellationToken);

        return Result.Success(new PlatformSubscriptionDto(
            subscription.Id,
            subscription.TenantId,
            tenantName,
            plan.Code,
            plan.Name,
            subscription.Status.ToString(),
            subscription.ActivationSource.ToString(),
            subscription.PaymentConfirmed,
            subscription.StartDate,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.CreatedAt));
    }
}
