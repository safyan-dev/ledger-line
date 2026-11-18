using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Tenancy.Application;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Modules.Tenancy.Infrastructure;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Tenancy;

public static class TenancyModule
{
    public static IServiceCollection AddTenancyModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCqrsHandlers(typeof(Tenant).Assembly);
        services.AddScoped<ITenancyResolver, TenantResolutionService>();
        services.AddScoped<ITenantMembershipVerifier, TenantMembershipVerifier>();
        services.AddScoped<TenantRegistrationService>();
        services.AddScoped<TenantQueries>();
        services.AddScoped<ITenantDirectory, TenantDirectory>();
        services.AddScoped<IPlanCatalog, PlanCatalog>();
        services.AddScoped<PlanSeeder>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IFeatureGate, FeatureGate>();
        services.AddScoped<IWebhookSecretStore, WebhookSecretService>();
        services.AddScoped<ITenantAuthorizer, TenantAuthorizer>();
        services.AddScoped<UsageService>();
        services.AddScoped<IUsageService>(provider => provider.GetRequiredService<UsageService>());
        services.AddScoped<IUsageLimitEnforcer>(provider => provider.GetRequiredService<UsageService>());
        services.AddScoped<IUsageCounterSource, TeamUsageCounterSource>();
        services.AddScoped<SubscriptionExpiryService>();
        services.AddHostedService<SubscriptionExpiryWorker>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.TenantMember, TenancyAuthorization.TenantScoped);
            options.AddPolicy(AuthorizationPolicies.TenantOwner, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new TenantOwnerRequirement()));
            options.AddPolicy(AuthorizationPolicies.TenantOwnerOrAdmin, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new TenantOwnerOrAdminRequirement()));
        });

        services.AddScoped<IAuthorizationHandler, TenantMemberAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, TenantOwnerAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, TenantOwnerOrAdminAuthorizationHandler>();

        return services;
    }
}
