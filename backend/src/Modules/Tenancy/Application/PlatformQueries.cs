using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record PlatformTenantDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    int MemberCount,
    string? PlanCode,
    string? PlanName,
    DateTimeOffset CreatedAt,
    string? OwnerEmail = null);

public sealed record PlatformTenantOwnerDto(Guid Id, string Name, string Email);

public sealed record PlatformTenantDetailsDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string? BusinessContactEmail,
    string DefaultCurrency,
    string TimeZone,
    DateTimeOffset CreatedAt,
    int MemberCount,
    PlatformTenantOwnerDto? Owner,
    string? PlanCode,
    string? PlanName,
    string? SubscriptionStatus,
    UsageSummaryDto? Usage);

public sealed record PlatformRecentTenantDto(
    Guid Id,
    string Name,
    string? OwnerEmail,
    string? PlanName,
    DateTimeOffset CreatedAt);

public sealed record PlatformAttentionSubscriptionDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string PlanName,
    string Status,
    DateTimeOffset? CurrentPeriodEnd);

public sealed record PlatformDashboardDto(
    int TotalTenants,
    int ActiveTenants,
    int SuspendedTenants,
    int ActiveSubscriptions,
    int TrialSubscriptions,
    IReadOnlyList<PlatformRecentTenantDto> RecentRegistrations,
    IReadOnlyList<PlatformAttentionSubscriptionDto> SubscriptionsRequiringAttention);

public sealed record GetPlatformDashboardQuery : IQuery<PlatformDashboardDto>;

public sealed class GetPlatformDashboardHandler(
    IAppDbContext dbContext,
    IIdentityFacade identityFacade)
    : IQueryHandler<GetPlatformDashboardQuery, PlatformDashboardDto>
{
    public async Task<Result<PlatformDashboardDto>> Handle(
        GetPlatformDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var tenants = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .OrderByDescending(tenant => tenant.CreatedAt)
            .ToListAsync(cancellationToken);

        var subscriptions = await dbContext.Set<Subscription>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(tenant => tenant.Id).ToArray();
        var memberships = await dbContext.Set<TenantUser>()
            .AsNoTracking()
            .Where(membership => tenantIds.Contains(membership.TenantId))
            .Select(membership => new { membership.TenantId, membership.UserId, membership.Role })
            .ToListAsync(cancellationToken);

        var ownerIds = memberships
            .Where(membership => membership.Role == TenantRole.Owner)
            .Select(membership => membership.UserId)
            .Distinct()
            .ToArray();
        var owners = (await identityFacade.GetUsersAsync(ownerIds, cancellationToken))
            .ToDictionary(user => user.Id);

        var planIds = subscriptions.Select(subscription => subscription.PlanId).Distinct().ToArray();
        var plans = await dbContext.Set<Plan>()
            .AsNoTracking()
            .Where(plan => planIds.Contains(plan.Id))
            .Select(plan => new { plan.Id, plan.Name })
            .ToDictionaryAsync(plan => plan.Id, cancellationToken);
        var tenantById = tenants.ToDictionary(tenant => tenant.Id);
        var subscriptionByTenant = subscriptions
            .GroupBy(subscription => subscription.TenantId)
            .ToDictionary(group => group.Key, group => group.First());
        var ownerByTenant = memberships
            .Where(membership => membership.Role == TenantRole.Owner)
            .GroupBy(membership => membership.TenantId)
            .ToDictionary(
                group => group.Key,
                group => owners.GetValueOrDefault(group.First().UserId)?.Email);

        var recent = tenants.Take(5).Select(tenant =>
        {
            subscriptionByTenant.TryGetValue(tenant.Id, out var subscription);
            return new PlatformRecentTenantDto(
                tenant.Id,
                tenant.Name,
                ownerByTenant.GetValueOrDefault(tenant.Id),
                subscription is not null && plans.TryGetValue(subscription.PlanId, out var plan)
                    ? plan.Name
                    : null,
                tenant.CreatedAt);
        }).ToList();

        var now = DateTimeOffset.UtcNow;
        var attention = subscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.PastDue
                || subscription.Status == SubscriptionStatus.Suspended
                || (subscription.Status == SubscriptionStatus.Trialing
                    && subscription.CurrentPeriodEnd is { } end
                    && end <= now.AddDays(7)))
            .Take(10)
            .Where(subscription => tenantById.ContainsKey(subscription.TenantId))
            .Select(subscription => new PlatformAttentionSubscriptionDto(
                subscription.Id,
                subscription.TenantId,
                tenantById[subscription.TenantId].Name,
                plans.GetValueOrDefault(subscription.PlanId)?.Name ?? "Unknown plan",
                subscription.Status.ToString(),
                subscription.CurrentPeriodEnd))
            .ToList();

        return Result.Success(new PlatformDashboardDto(
            tenants.Count,
            tenants.Count(tenant => tenant.Status == TenantStatus.Active),
            tenants.Count(tenant => tenant.Status == TenantStatus.Suspended),
            subscriptions.Count(subscription => subscription.Status == SubscriptionStatus.Active),
            subscriptions.Count(subscription => subscription.Status == SubscriptionStatus.Trialing),
            recent,
            attention));
    }
}

public sealed record GetPlatformTenantsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? Status = null) : IQuery<PagedResult<PlatformTenantDto>>;

public sealed class GetPlatformTenantsHandler(
    IAppDbContext dbContext,
    IIdentityFacade identityFacade)
    : IQueryHandler<GetPlatformTenantsQuery, PagedResult<PlatformTenantDto>>
{
    public async Task<Result<PagedResult<PlatformTenantDto>>> Handle(
        GetPlatformTenantsQuery query,
        CancellationToken cancellationToken)
    {
        // Paginate tenant list
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var tenantQuery = dbContext.Set<Tenant>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<TenantStatus>(query.Status, true, out var status)
                || !Enum.IsDefined(status))
            {
                return Result.Failure<PagedResult<PlatformTenantDto>>(Error.Validation(
                    "tenant.invalid_status",
                    "Tenant status is invalid."));
            }

            tenantQuery = tenantQuery.Where(tenant => tenant.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Limit search length
            var trimmed = query.Search.Trim();
            var search = trimmed[..Math.Min(100, trimmed.Length)];
            tenantQuery = tenantQuery.Where(tenant =>
                tenant.Name.Contains(search)
                || tenant.Slug.Contains(search));
        }

        var totalCount = await tenantQuery.CountAsync(cancellationToken);
        var tenants = await tenantQuery
            .OrderBy(tenant => tenant.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var tenantIds = tenants.Select(tenant => tenant.Id).ToArray();

        var memberships = await dbContext.Set<TenantUser>()
            .AsNoTracking()
            .Where(membership => tenantIds.Contains(membership.TenantId))
            .Select(membership => new { membership.TenantId, membership.UserId, membership.Role })
            .ToListAsync(cancellationToken);
        var memberCounts = memberships
            .GroupBy(membership => membership.TenantId)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownerIds = memberships
            .Where(membership => membership.Role == TenantRole.Owner)
            .Select(membership => membership.UserId)
            .Distinct()
            .ToArray();
        var owners = (await identityFacade.GetUsersAsync(ownerIds, cancellationToken))
            .ToDictionary(user => user.Id);
        var ownerByTenant = memberships
            .Where(membership => membership.Role == TenantRole.Owner)
            .GroupBy(membership => membership.TenantId)
            .ToDictionary(
                group => group.Key,
                group => owners.GetValueOrDefault(group.First().UserId)?.Email);

        var subscriptions = await dbContext.Set<Subscription>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(subscription => tenantIds.Contains(subscription.TenantId))
            .Select(subscription => new { subscription.TenantId, subscription.PlanId })
            .ToListAsync(cancellationToken);

        var subscriptionsByTenant = subscriptions
            .GroupBy(subscription => subscription.TenantId)
            .ToDictionary(group => group.Key, group => group.First());

        var planIds = subscriptions.Select(subscription => subscription.PlanId).Distinct().ToList();
        var plans = await dbContext.Set<Plan>()
            .AsNoTracking()
            .Where(plan => planIds.Contains(plan.Id))
            .Select(plan => new { plan.Id, plan.Code, plan.Name })
            .ToDictionaryAsync(plan => plan.Id, cancellationToken);

        var items = tenants
            .Select(tenant =>
            {
                subscriptionsByTenant.TryGetValue(tenant.Id, out var subscription);
                var plan = subscription is not null && plans.TryGetValue(subscription.PlanId, out var found)
                    ? found
                    : null;

                return new PlatformTenantDto(
                    tenant.Id,
                    tenant.Name,
                    tenant.Slug,
                    tenant.Status.ToString(),
                    memberCounts.GetValueOrDefault(tenant.Id),
                    plan?.Code,
                    plan?.Name,
                    tenant.CreatedAt,
                    ownerByTenant.GetValueOrDefault(tenant.Id));
            })
            .ToList();

        return Result.Success(PagedResult<PlatformTenantDto>.Create(
            items,
            page,
            pageSize,
            totalCount));
    }
}

public sealed record GetPlatformTenantDetailsQuery(Guid TenantId) : IQuery<PlatformTenantDetailsDto>;

public sealed class GetPlatformTenantDetailsHandler(
    IAppDbContext dbContext,
    IEnumerable<IUsageCounterSource> usageCounterSources,
    IIdentityFacade identityFacade)
    : IQueryHandler<GetPlatformTenantDetailsQuery, PlatformTenantDetailsDto>
{
    public async Task<Result<PlatformTenantDetailsDto>> Handle(
        GetPlatformTenantDetailsQuery query,
        CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<PlatformTenantDetailsDto>(Error.NotFound(
                "tenant.not_found",
                "The tenant was not found."));
        }

        var memberships = await dbContext.Set<TenantUser>()
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenant.Id)
            .ToListAsync(cancellationToken);
        var ownerId = memberships
            .Where(membership => membership.Role == TenantRole.Owner)
            .Select(membership => (Guid?)membership.UserId)
            .FirstOrDefault();
        PlatformTenantOwnerDto? owner = null;
        if (ownerId is { } id)
        {
            var user = (await identityFacade.GetUsersAsync([id], cancellationToken))
                .FirstOrDefault();
            if (user is not null)
            {
                owner = new PlatformTenantOwnerDto(user.Id, user.DisplayName, user.Email);
            }
        }

        var subscription = await dbContext.Set<Subscription>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenant.Id, cancellationToken);
        Plan? plan = null;
        UsageSummaryDto? usage = null;
        if (subscription is not null)
        {
            plan = await dbContext.Set<Plan>()
                .AsNoTracking()
                .Include(candidate => candidate.Features)
                .ThenInclude(feature => feature.Feature)
                .Include(candidate => candidate.Limits)
                .FirstOrDefaultAsync(candidate => candidate.Id == subscription.PlanId, cancellationToken);
            if (plan is not null)
            {
                var now = DateTimeOffset.UtcNow;
                var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                var periodEnd = periodStart.AddMonths(1);
                var sources = usageCounterSources.ToDictionary(source => source.Metric);
                var definitions = new[]
                {
                    (Metric: UsageMetric.OrdersPerMonth, Key: "orders_per_month", Label: "Orders", Period: periodStart.ToString("yyyy-MM")),
                    (Metric: UsageMetric.PaymentsPerMonth, Key: "payments_per_month", Label: "Payments", Period: periodStart.ToString("yyyy-MM")),
                    (Metric: UsageMetric.TeamMembers, Key: "team_members", Label: "Team members", Period: "current")
                };
                var metrics = new List<UsageMetricDto>(definitions.Length);
                foreach (var definition in definitions)
                {
                    var limit = plan.Limits.FirstOrDefault(candidate =>
                        string.Equals(candidate.Key, definition.Key, StringComparison.OrdinalIgnoreCase));
                    var used = sources.TryGetValue(definition.Metric, out var source)
                        ? await source.CountAsync(tenant.Id, periodStart, periodEnd, cancellationToken)
                        : 0;
                    metrics.Add(new UsageMetricDto(
                        definition.Metric.ToString(),
                        limit?.Label ?? definition.Label,
                        used,
                        limit?.Value,
                        limit?.Value is { } limitValue ? limitValue - used : null,
                        definition.Period));
                }

                usage = new UsageSummaryDto(
                    plan.Code,
                    plan.Name,
                    subscription.Status.ToString(),
                    periodStart,
                    periodEnd,
                    metrics,
                    PlanFormatting.OrderFeatures(plan.Features));
            }
        }

        return Result.Success(new PlatformTenantDetailsDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.BusinessContactEmail,
            tenant.DefaultCurrency,
            tenant.TimeZone,
            tenant.CreatedAt,
            memberships.Count,
            owner,
            plan?.Code,
            plan?.Name,
            subscription?.Status.ToString(),
            usage));
    }
}
