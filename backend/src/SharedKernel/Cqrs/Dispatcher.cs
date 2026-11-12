using System.Collections.Concurrent;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentReconciliation.SharedKernel.Cqrs;

public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypeCache = new();

    public Task<Results.Result<TResponse>> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerType = HandlerTypeCache.GetOrAdd(requestType, ResolveHandlerType);

        var handler = serviceProvider.GetRequiredService(handlerType);
        var handleMethod = handlerType.GetMethod("Handle")
            ?? throw new InvalidOperationException($"Handler {handlerType.Name} has no Handle method.");

        RequestHandlerDelegate<TResponse> pipeline = () => (Task<Results.Result<TResponse>>)handleMethod.Invoke(handler, [request, cancellationToken])!;

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = serviceProvider.GetServices(behaviorType).OfType<object>().ToArray();

        for (var index = behaviors.Length - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var method = behavior.GetType().GetMethod("HandleAsync")
                         ?? throw new InvalidOperationException($"Behavior {behavior.GetType().Name} has no HandleAsync method.");
            var current = pipeline;
            pipeline = () => (Task<Results.Result<TResponse>>)method.Invoke(behavior, [request, current, cancellationToken])!;
        }

        return pipeline();
    }

    private static Type ResolveHandlerType(Type requestType)
    {
        var requestInterface = requestType
            .GetInterfaces()
            .FirstOrDefault(candidate =>
                candidate.IsGenericType
                && (candidate.GetGenericTypeDefinition() == typeof(ICommand<>)
                    || candidate.GetGenericTypeDefinition() == typeof(IQuery<>)));

        if (requestInterface is null)
        {
            throw new InvalidOperationException(
                $"Request {requestType.Name} does not implement ICommand<> or IQuery<>.");
        }

        var responseType = requestInterface.GetGenericArguments()[0];
        var handlerDefinition = requestInterface.GetGenericTypeDefinition() == typeof(ICommand<>)
            ? typeof(ICommandHandler<,>)
            : typeof(IQueryHandler<,>);

        return handlerDefinition.MakeGenericType(requestType, responseType);
    }
}

public static class CqrsRegistration
{
    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies.Distinct())
        {
            foreach (var type in assembly.GetTypes().Where(IsConcrete))
            {
                foreach (var implementedInterface in type.GetInterfaces().Where(IsHandlerInterface))
                {
                    services.AddScoped(implementedInterface, type);
                }

                foreach (var validatorInterface in type.GetInterfaces().Where(IsValidatorInterface))
                {
                    services.AddScoped(validatorInterface, type);
                }
            }
        }

        return services;
    }

    private static bool IsConcrete(Type type)
        => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false };

    private static bool IsHandlerInterface(Type type)
        => type.IsGenericType
           && (type.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
               || type.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)
               || type.GetGenericTypeDefinition() == typeof(Events.IDomainEventHandler<>));

    private static bool IsValidatorInterface(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IValidator<>);
}
