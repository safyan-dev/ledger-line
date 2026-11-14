using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Support.Domain;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Support;

public static class SupportModule
{
    public static IServiceCollection AddSupportModule(this IServiceCollection services)
    {
        services.AddCqrsHandlers(typeof(SupportTicket).Assembly);
        return services;
    }
}
