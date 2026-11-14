using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Orders.Application;
using PaymentReconciliation.Modules.Orders.Contracts;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Modules.Orders.Infrastructure;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Orders;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddCqrsHandlers(typeof(Order).Assembly);
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrdersFacade, OrdersFacade>();
        services.AddScoped<IOrderReporting, OrderReporting>();
        services.AddScoped<IUsageCounterSource, OrderUsageCounterSource>();
        return services;
    }
}
