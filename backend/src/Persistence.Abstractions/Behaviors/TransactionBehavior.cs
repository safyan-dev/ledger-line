using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Persistence.Abstractions.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICommandMarker || unitOfWork.HasActiveTransaction)
        {
            return await next().ConfigureAwait(false);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await next().ConfigureAwait(false);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }

            var iterations = 0;
            while (unitOfWork.HasPendingDomainEvents && iterations++ < 10)
            {
                var dispatchResult = await unitOfWork.DispatchDomainEventsAsync(cancellationToken).ConfigureAwait(false);
                if (dispatchResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Result.Failure<TResponse>(dispatchResult.Error!);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
