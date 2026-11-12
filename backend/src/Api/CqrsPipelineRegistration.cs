using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Persistence.Abstractions.Behaviors;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Api;

public static class CqrsPipelineRegistration
{
    public static IServiceCollection AddCqrsPipeline(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ExceptionToResultBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
