using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record GetSubscriptionQuery : IQuery<SubscriptionDto>;

public sealed class GetSubscriptionHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAuthorizer authorizer)
    : IQueryHandler<GetSubscriptionQuery, SubscriptionDto>
{
    public async Task<Result<SubscriptionDto>> Handle(
        GetSubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden("tenant.required", "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ViewSubscription);
        if (authorization.IsFailure)
        {
            return Result.Failure<SubscriptionDto>(authorization.Error!);
        }

        var subscription = await dbContext.Set<Subscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."));
        }

        return Result.Success(await ToDtoAsync(subscription, dbContext, cancellationToken));
    }

    internal static async Task<SubscriptionDto> ToDtoAsync(
        Subscription subscription,
        IAppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.Set<Plan>()
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == subscription.PlanId, cancellationToken);

        string? pendingPlanCode = null;
        if (subscription.PendingPlanId is { } pendingPlanId)
        {
            pendingPlanCode = await dbContext.Set<Plan>()
                .AsNoTracking()
                .Where(candidate => candidate.Id == pendingPlanId)
                .Select(candidate => candidate.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new SubscriptionDto(
            subscription.Id,
            plan.Code,
            plan.Name,
            subscription.Status.ToString(),
            subscription.ActivationSource.ToString(),
            subscription.PaymentConfirmed,
            subscription.StartDate,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd,
            pendingPlanCode,
            subscription.PendingPlanEffectiveAt);
    }
}

public sealed record ChangeSubscriptionPlanCommand(string PlanCode) : ICommand<SubscriptionDto>;

public sealed class ChangeSubscriptionPlanHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    IPlanCatalog planCatalog,
    ITenantAuthorizer authorizer)
    : ICommandHandler<ChangeSubscriptionPlanCommand, SubscriptionDto>
{
    public async Task<Result<SubscriptionDto>> Handle(
        ChangeSubscriptionPlanCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden("tenant.required", "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ManageSubscription);
        if (authorization.IsFailure)
        {
            return Result.Failure<SubscriptionDto>(authorization.Error!);
        }

        var subscription = await dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."));
        }

        if (subscription.Status == SubscriptionStatus.Suspended)
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden(
                "subscription.suspended",
                "This subscription is suspended. Contact support to restore access."));
        }

        var targetPlan = await planCatalog.FindPlanAsync(command.PlanCode, cancellationToken);
        if (targetPlan is null || !targetPlan.IsActive)
        {
            return Result.Failure<SubscriptionDto>(Error.Validation(
                "plan.not_found",
                $"Plan '{command.PlanCode}' is not available."));
        }

        if (targetPlan.Id == subscription.PlanId)
        {
            return Result.Failure<SubscriptionDto>(Error.Conflict(
                "subscription.same_plan",
                "This tenant is already on that plan."));
        }

        var currentPlan = await dbContext.Set<Plan>()
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == subscription.PlanId, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var isInactive = subscription.Status is SubscriptionStatus.Expired
            or SubscriptionStatus.Canceled
            or SubscriptionStatus.Suspended
            or SubscriptionStatus.PastDue;

        if (targetPlan.SortOrder > currentPlan.SortOrder || isInactive)
        {
            subscription.ChangePlanImmediate(
                targetPlan.Id,
                targetPlan.IsFree,
                SubscriptionActivation.Simulated,
                paymentConfirmed: targetPlan.IsFree,
                now);

            dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
                tenantId,
                subscription.Id,
                targetPlan.Id,
                "subscription.upgraded",
                subscription.Status,
                previousPlanId: currentPlan.Id,
                note: $"Upgraded from {currentPlan.Name} to {targetPlan.Name}."
                      + (targetPlan.IsFree ? string.Empty : " Simulated activation; no payment collected."),
                occurredAt: now));
        }
        else if (subscription.CurrentPeriodEnd is null)
        {
            subscription.ChangePlanImmediate(
                targetPlan.Id,
                targetPlan.IsFree,
                targetPlan.IsFree ? SubscriptionActivation.Free : SubscriptionActivation.Simulated,
                paymentConfirmed: targetPlan.IsFree,
                now);

            dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
                tenantId,
                subscription.Id,
                targetPlan.Id,
                "subscription.changed",
                subscription.Status,
                previousPlanId: currentPlan.Id,
                note: $"Changed from {currentPlan.Name} to {targetPlan.Name}.",
                occurredAt: now));
        }
        else
        {
            subscription.SchedulePlanChange(targetPlan.Id, now);

            dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
                tenantId,
                subscription.Id,
                targetPlan.Id,
                "subscription.downgrade_scheduled",
                subscription.Status,
                previousPlanId: currentPlan.Id,
                note: $"Downgrade from {currentPlan.Name} to {targetPlan.Name} takes effect at the end of the current period.",
                occurredAt: now));
        }

        return Result.Success(await GetSubscriptionHandler.ToDtoAsync(subscription, dbContext, cancellationToken));
    }
}

public sealed record CancelSubscriptionCommand : ICommand<SubscriptionDto>;

public sealed class CancelSubscriptionHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAuthorizer authorizer)
    : ICommandHandler<CancelSubscriptionCommand, SubscriptionDto>
{
    public async Task<Result<SubscriptionDto>> Handle(
        CancelSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden("tenant.required", "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ManageSubscription);
        if (authorization.IsFailure)
        {
            return Result.Failure<SubscriptionDto>(authorization.Error!);
        }

        var subscription = await dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."));
        }

        var now = DateTimeOffset.UtcNow;
        subscription.CancelAtPeriodEndNow(now);

        var isImmediate = subscription.Status == SubscriptionStatus.Canceled;

        dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
            tenantId,
            subscription.Id,
            subscription.PlanId,
            isImmediate ? "subscription.canceled" : "subscription.cancel_scheduled",
            subscription.Status,
            note: isImmediate
                ? "Subscription canceled."
                : "Cancellation takes effect at the end of the current period.",
            occurredAt: now));

        return Result.Success(await GetSubscriptionHandler.ToDtoAsync(subscription, dbContext, cancellationToken));
    }
}

public sealed record ReactivateSubscriptionCommand : ICommand<SubscriptionDto>;

public sealed class ReactivateSubscriptionHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAuthorizer authorizer)
    : ICommandHandler<ReactivateSubscriptionCommand, SubscriptionDto>
{
    public async Task<Result<SubscriptionDto>> Handle(
        ReactivateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden("tenant.required", "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ManageSubscription);
        if (authorization.IsFailure)
        {
            return Result.Failure<SubscriptionDto>(authorization.Error!);
        }

        var subscription = await dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."));
        }

        if (subscription.Status == SubscriptionStatus.Suspended)
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden(
                "subscription.suspended",
                "This subscription is suspended. Contact support to restore access."));
        }

        var plan = await dbContext.Set<Plan>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == subscription.PlanId, cancellationToken);

        if (plan is null)
        {
            return Result.Failure<SubscriptionDto>(Error.NotFound(
                "plan.not_found",
                "The subscription plan was not found."));
        }

        var now = DateTimeOffset.UtcNow;
        var wasExpired = subscription.Status is SubscriptionStatus.Expired or SubscriptionStatus.Canceled;

        if (!subscription.Reactivate(now, plan.MonthlyPrice == 0m))
        {
            return Result.Failure<SubscriptionDto>(Error.Conflict(
                "subscription.not_reactivatable",
                "This subscription cannot be reactivated in its current state."));
        }

        dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
            tenantId,
            subscription.Id,
            subscription.PlanId,
            "subscription.reactivated",
            subscription.Status,
            note: wasExpired
                ? "Subscription reactivated. A new period was started."
                : "Scheduled cancellation cleared.",
            occurredAt: now));

        return Result.Success(await GetSubscriptionHandler.ToDtoAsync(subscription, dbContext, cancellationToken));
    }
}

public sealed record GetSubscriptionHistoryQuery(int Page = 1, int PageSize = 25)
    : IQuery<PagedResult<SubscriptionHistoryDto>>;

public sealed class GetSubscriptionHistoryHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAuthorizer authorizer)
    : IQueryHandler<GetSubscriptionHistoryQuery, PagedResult<SubscriptionHistoryDto>>
{
    public async Task<Result<PagedResult<SubscriptionHistoryDto>>> Handle(
        GetSubscriptionHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<PagedResult<SubscriptionHistoryDto>>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ViewSubscription);
        if (authorization.IsFailure)
        {
            return Result.Failure<PagedResult<SubscriptionHistoryDto>>(authorization.Error!);
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var history = dbContext.Set<SubscriptionHistory>()
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId);

        var totalCount = await history.CountAsync(cancellationToken);
        var rows = await history
            .OrderByDescending(row => row.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var planIds = rows
            .Select(row => row.PlanId)
            .Concat(rows.Where(row => row.PreviousPlanId is not null).Select(row => row.PreviousPlanId!.Value))
            .Distinct()
            .ToList();

        var plans = await dbContext.Set<Plan>()
            .AsNoTracking()
            .Where(plan => planIds.Contains(plan.Id))
            .Select(plan => new { plan.Id, plan.Code, plan.Name })
            .ToDictionaryAsync(plan => plan.Id, cancellationToken);

        var items = rows
            .Select(row => new SubscriptionHistoryDto(
                row.Id,
                row.Event,
                row.Status,
                plans.TryGetValue(row.PlanId, out var plan) ? plan.Code : string.Empty,
                plans.TryGetValue(row.PlanId, out plan) ? plan.Name : string.Empty,
                row.PreviousPlanId is { } previousPlanId && plans.TryGetValue(previousPlanId, out var previousPlan)
                    ? previousPlan.Code
                    : null,
                row.Note,
                row.OccurredAt))
            .ToList();

        return Result.Success(PagedResult<SubscriptionHistoryDto>.Create(items, page, pageSize, totalCount));
    }
}

public sealed record RenewSubscriptionCommand(string IdempotencyKey) : ICommand<SubscriptionDto>;

public sealed class RenewSubscriptionHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAuthorizer authorizer)
    : ICommandHandler<RenewSubscriptionCommand, SubscriptionDto>
{
    public async Task<Result<SubscriptionDto>> Handle(
        RenewSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            return Result.Failure<SubscriptionDto>(Error.Validation(
                "subscription.idempotency_required",
                "An idempotency key is required for renewal."));
        }

        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden("tenant.required", "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.ManageSubscription);
        if (authorization.IsFailure)
        {
            return Result.Failure<SubscriptionDto>(authorization.Error!);
        }

        var subscription = await dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."));
        }

        var key = command.IdempotencyKey.Trim();
        var alreadyRenewed = await dbContext.Set<SubscriptionHistory>()
            .AnyAsync(history => history.TenantId == tenantId && history.IdempotencyKey == key, cancellationToken);

        if (alreadyRenewed)
        {
            return Result.Success(await GetSubscriptionHandler.ToDtoAsync(subscription, dbContext, cancellationToken));
        }

        var now = DateTimeOffset.UtcNow;
        if (!subscription.Renew(now))
        {
            return Result.Failure<SubscriptionDto>(Error.Conflict(
                "subscription.not_renewable",
                "This subscription cannot be renewed in its current state."));
        }

        dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
            tenantId,
            subscription.Id,
            subscription.PlanId,
            "subscription.renewed",
            subscription.Status,
            note: "Simulated renewal; no payment collected.",
            idempotencyKey: key,
            occurredAt: now));

        return Result.Success(await GetSubscriptionHandler.ToDtoAsync(subscription, dbContext, cancellationToken));
    }
}

public sealed record GetEntitlementsQuery : IQuery<EntitlementsDto>;

public sealed class GetEntitlementsHandler(
    IEntitlementService entitlements,
    ITenantAuthorizer authorizer)
    : IQueryHandler<GetEntitlementsQuery, EntitlementsDto>
{
    public async Task<Result<EntitlementsDto>> Handle(
        GetEntitlementsQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = authorizer.Require(TenantPermission.ViewUsage);
        if (authorization.IsFailure)
        {
            return Result.Failure<EntitlementsDto>(authorization.Error!);
        }

        var result = await entitlements.GetEntitlementsAsync(cancellationToken);

        return result is null
            ? Result.Failure<EntitlementsDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."))
            : Result.Success(result);
    }
}
