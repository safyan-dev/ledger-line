using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Payments.Application;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Modules.Payments.Infrastructure;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        services.AddCqrsHandlers(typeof(Payment).Assembly);
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IPaymentsFacade, PaymentsFacade>();
        services.AddScoped<IPaymentReporting, PaymentReporting>();
        return services;
    }
}
