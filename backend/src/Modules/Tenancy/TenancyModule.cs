using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Tenancy.Application;
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
        services.AddScoped<TenantRegistrationService>();
        services.AddScoped<TenantQueries>();
        services.AddScoped<IPlanCatalog, PlanCatalog>();
        services.AddScoped<PlanSeeder>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<SubscriptionExpiryService>();
        services.AddHostedService<SubscriptionExpiryWorker>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.TenantMember, TenancyAuthorization.TenantScoped);
            options.AddPolicy(AuthorizationPolicies.TenantOwnerOrAdmin, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new TenantOwnerOrAdminRequirement()));
        });

        services.AddScoped<IAuthorizationHandler, TenantMemberAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, TenantOwnerOrAdminAuthorizationHandler>();

        return services;
    }
}
