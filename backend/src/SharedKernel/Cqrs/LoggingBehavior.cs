using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.SharedKernel.Cqrs;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var result = await next().ConfigureAwait(false);
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "{RequestName} failed with {ErrorCode} in {ElapsedMs:0.0} ms",
                    requestName,
                    result.Error!.Code,
                    elapsedMs);
            }
            else
            {
                logger.LogInformation("{RequestName} handled in {ElapsedMs:0.0} ms", requestName, elapsedMs);
            }

            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "{RequestName} threw after {ElapsedMs:0.0} ms",
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }
}
