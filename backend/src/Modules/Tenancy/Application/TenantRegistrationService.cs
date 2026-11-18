using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed class TenantRegistrationService(
    IIdentityFacade identity,
    IPlanCatalog planCatalog,
    IAppDbContext dbContext,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IWebhookSecretStore webhookSecrets)
{
    public async Task<Result<RegisterTenantResponse>> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Tenant.IsValidSlug(request.TenantSlug))
        {
            return Result.Failure<RegisterTenantResponse>(Error.Validation(
                "tenant.invalid_slug",
                "Tenant slug must be 2-40 characters and use only letters, digits, and hyphens."));
        }

        var slug = Tenant.NormalizeSlug(request.TenantSlug);
        if (await dbContext.Set<Tenant>().AnyAsync(tenant => tenant.Slug == slug, cancellationToken))
        {
            return Result.Failure<RegisterTenantResponse>(Error.Conflict(
                "tenant.slug_taken",
                $"Tenant slug '{slug}' is already in use."));
        }

        var planCode = string.IsNullOrWhiteSpace(request.PlanCode)
            ? "free"
            : request.PlanCode.Trim().ToLowerInvariant();

        var plan = await planCatalog.FindPlanAsync(planCode, cancellationToken);
        if (plan is null || !plan.IsActive)
        {
            return Result.Failure<RegisterTenantResponse>(Error.Validation(
                "plan.not_found",
                $"Plan '{planCode}' is not available."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var user = await identity.CreateUserAsync(
            request.Email,
            request.Password,
            request.OwnerName,
            cancellationToken);
        if (user.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<RegisterTenantResponse>(user.Error!);
        }

        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create(request.CompanyName, slug, now);
        var membership = TenantUser.Create(tenant.Id, user.Value.Id, TenantRole.Owner);

        tenantContext.Set(new TenantContextSnapshot(
            tenant.Id,
            tenant.Slug,
            user.Value.Id,
            TenantRole.Owner.ToString(),
            user.Value.IsPlatformAdmin));

        dbContext.Set<Tenant>().Add(tenant);
        dbContext.Set<TenantUser>().Add(membership);
        await webhookSecrets.EnsureSecretsAsync(tenant.Id, cancellationToken);

        var activation = plan.IsFree ? SubscriptionActivation.Free : SubscriptionActivation.Simulated;
        var subscription = Subscription.Provision(tenant.Id, plan.Id, plan.IsFree, activation, now);
        dbContext.Set<Subscription>().Add(subscription);
        dbContext.Set<SubscriptionHistory>().Add(SubscriptionHistory.Create(
            tenant.Id,
            subscription.Id,
            plan.Id,
            "subscription.activated",
            subscription.Status,
            note: plan.IsFree
                ? "Free plan assigned."
                : "Simulated activation; no payment collected.",
            occurredAt: now));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        TenantSessionDto? session = null;
        if (user.Value.EmailConfirmed)
        {
            var issued = await identity.IssueSessionAsync(user.Value.Id, cancellationToken);
            if (issued.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<RegisterTenantResponse>(issued.Error!);
            }

            session = new TenantSessionDto(
                issued.Value.AccessToken,
                issued.Value.RefreshToken,
                issued.Value.AccessTokenExpiresAt,
                issued.Value.RefreshTokenExpiresAt,
                issued.Value.User);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new RegisterTenantResponse(
            session,
            new TenantDto(tenant.Id, tenant.Name, tenant.Slug, membership.Role.ToString()),
            plan.Code,
            plan.Name,
            EmailVerificationRequired: !user.Value.EmailConfirmed));
    }
}
