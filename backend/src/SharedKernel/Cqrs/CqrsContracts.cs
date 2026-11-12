namespace PaymentReconciliation.SharedKernel.Cqrs;

public interface IRequest<out TResponse>
{
}

public interface ICommandMarker
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>, ICommandMarker
{
}

public interface IQuery<out TResponse> : IRequest<TResponse>
{
}

public delegate Task<Results.Result<TResponse>> RequestHandlerDelegate<TResponse>();

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Results.Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Results.Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}

public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<Results.Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<Results.Result<TResponse>> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
