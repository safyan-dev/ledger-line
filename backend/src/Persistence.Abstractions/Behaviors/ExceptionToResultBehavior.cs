using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Domain;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Persistence.Abstractions.Behaviors;

public sealed class ExceptionToResultBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (DomainException exception)
        {
            return Result.Failure<TResponse>(
                Error.Validation("domain.rule_violation", exception.Message));
        }
        catch (NotFoundException exception)
        {
            return Result.Failure<TResponse>(Error.NotFound(exception.Code, exception.Message));
        }
        catch (ConflictException exception)
        {
            return Result.Failure<TResponse>(Error.Conflict(exception.Code, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<TResponse>(
                Error.Conflict("conflict.concurrency", "The record changed while it was being saved. Reload and try again."));
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            return Result.Failure<TResponse>(
                Error.Conflict("conflict.duplicate", postgres.Detail ?? "A record with the same unique values already exists."));
        }
    }
}
