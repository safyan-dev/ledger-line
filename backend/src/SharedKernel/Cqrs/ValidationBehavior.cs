using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.SharedKernel.Cqrs;

public sealed class ValidationBehavior<TRequest, TResponse>(IServiceProvider serviceProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validator = serviceProvider.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return await next().ConfigureAwait(false);
        }

        var validationResult = await validator
            .ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)
            .ConfigureAwait(false);

        if (validationResult.IsValid)
        {
            return await next().ConfigureAwait(false);
        }

        var failures = validationResult.Errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? "request" : error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        return Result.Failure<TResponse>(
            Error.Validation("validation.failed", "One or more validation errors occurred.", failures));
    }
}
