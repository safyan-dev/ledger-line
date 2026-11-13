using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record GetSubscriptionQuery : IQuery<SubscriptionDto>;

public sealed class GetSubscriptionHandler(IAppDbContext dbContext, ITenantContext tenantContext)
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
    IPlanCatalog planCatalog)
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

        if (!TenantRoleRules.TryParse(tenantContext.Role, out var actorRole)
            || !TenantRoleRules.CanManageTenant(actorRole))
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden(
                "tenant.forbidden",
                "Only an owner or admin can change the subscription."));
        }

        var subscription = await dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."));
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

        if (targetPlan.SortOrder > currentPlan.SortOrder)
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
    ITenantContext tenantContext)
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

        if (!TenantRoleRules.TryParse(tenantContext.Role, out var actorRole)
            || !TenantRoleRules.CanManageTenant(actorRole))
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden(
                "tenant.forbidden",
                "Only an owner or admin can cancel the subscription."));
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

        dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
            tenantId,
            subscription.Id,
            subscription.PlanId,
            "subscription.cancel_scheduled",
            subscription.Status,
            note: subscription.CurrentPeriodEnd is null
                ? "Cancellation requested."
                : "Cancellation takes effect at the end of the current period.",
            occurredAt: now));

        return Result.Success(await GetSubscriptionHandler.ToDtoAsync(subscription, dbContext, cancellationToken));
    }
}

public sealed record RenewSubscriptionCommand(string IdempotencyKey) : ICommand<SubscriptionDto>;

public sealed class RenewSubscriptionHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext)
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

        if (!TenantRoleRules.TryParse(tenantContext.Role, out var actorRole)
            || !TenantRoleRules.CanManageTenant(actorRole))
        {
            return Result.Failure<SubscriptionDto>(Error.Forbidden(
                "tenant.forbidden",
                "Only an owner or admin can renew the subscription."));
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

public sealed class GetEntitlementsHandler(IEntitlementService entitlements)
    : IQueryHandler<GetEntitlementsQuery, EntitlementsDto>
{
    public async Task<Result<EntitlementsDto>> Handle(
        GetEntitlementsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await entitlements.GetEntitlementsAsync(cancellationToken);

        return result is null
            ? Result.Failure<EntitlementsDto>(Error.NotFound(
                "subscription.not_found",
                "This tenant does not have a subscription."))
            : Result.Success(result);
    }
}
