using Microsoft.Extensions.DependencyInjection;

namespace PaymentReconciliation.SharedKernel.Events;

public interface IDomainEventHandler<in TEvent>
    where TEvent : Domain.IDomainEvent
{
    Task<Results.Result> HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

public interface IDomainEventDispatcher
{
    Task<Results.Result> PublishAsync(Domain.IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task<Results.Result> PublishAsync(
        Domain.IDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = serviceProvider.GetServices(handlerType).OfType<object>().ToArray();
        var handleMethod = handlerType.GetMethod("HandleAsync")
                           ?? throw new InvalidOperationException($"Handler for {domainEvent.GetType().Name} has no HandleAsync method.");

        foreach (var handler in handlers)
        {
            var task = (Task<Results.Result>)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
            var result = await task.ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Results.Result.Success();
    }
}
