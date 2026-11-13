using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Reconciliation.Application;
using PaymentReconciliation.Modules.Reconciliation.Contracts;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.Modules.Reconciliation.Infrastructure;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Reconciliation;

public static class ReconciliationModule
{
    public static IServiceCollection AddReconciliationModule(this IServiceCollection services)
    {
        services.AddCqrsHandlers(typeof(ReconciliationRecord).Assembly);
        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
        services.AddScoped<IPaymentMatcher, PaymentMatcher>();
        services.AddScoped<IReconciliationReporting, ReconciliationReporting>();
        return services;
    }
}
