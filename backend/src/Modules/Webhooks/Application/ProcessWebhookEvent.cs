using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Webhooks.Application;

public sealed record ProcessWebhookEventCommand(Guid WebhookEventId) : ICommand<Unit>;

public sealed class ProcessWebhookEventHandler(
    IWebhookEventRepository repository,
    IWebhookPayloadParser payloadParser,
    IPaymentsFacade paymentsFacade)
    : ICommandHandler<ProcessWebhookEventCommand, Unit>
{
    public async Task<Result<Unit>> Handle(
        ProcessWebhookEventCommand command,
        CancellationToken cancellationToken)
    {
        var webhookEvent = await repository.GetByIdAsync(command.WebhookEventId, cancellationToken);
        if (webhookEvent is null)
        {
            return Result.Failure<Unit>(
                Error.NotFound("webhook.not_found", $"Webhook event {command.WebhookEventId} was not found."));
        }

        if (webhookEvent.Status is WebhookStatus.Processed
            or WebhookStatus.Ignored
            or WebhookStatus.Failed
            or WebhookStatus.Conflict)
        {
            return Result.Success(Unit.Value);
        }

        webhookEvent.MarkProcessing();

        var parsedResult = payloadParser.Parse(webhookEvent.Provider, webhookEvent.Payload);
        if (parsedResult.IsFailure)
        {
            webhookEvent.MarkFailed(FormatReason(parsedResult.Error!));
            return Result.Success(Unit.Value);
        }

        var parsed = parsedResult.Value;

        if (parsed.Payment is null)
        {
            webhookEvent.MarkIgnored(parsed.IgnoredReason ?? "Event type is not handled.");
            return Result.Success(Unit.Value);
        }

        var succeeded = !string.Equals(parsed.EventType, "payment.failed", StringComparison.OrdinalIgnoreCase);

        var applyResult = await paymentsFacade.ApplyProviderPaymentAsync(
            parsed.Payment,
            succeeded,
            AuditActor.System,
            cancellationToken);

        if (applyResult.IsFailure)
        {
            webhookEvent.MarkFailed(applyResult.Error!.Message);
            return Result.Success(Unit.Value);
        }

        if (applyResult.Value.Conflict)
        {
            webhookEvent.MarkConflict(
                applyResult.Value.ConflictReason ?? "Provider payment conflicts with an existing payment.");
            return Result.Success(Unit.Value);
        }

        webhookEvent.MarkProcessed(applyResult.Value.Payment.Id);
        return Result.Success(Unit.Value);
    }

    private static string FormatReason(Error error)
    {
        var reason = error.Message;

        if (error.Failures is { Count: > 0 })
        {
            var details = string.Join(
                "; ",
                error.Failures.Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value)}"));
            reason = $"{reason} {details}";
        }

        return reason.Length <= 400 ? reason : reason[..400];
    }
}

public sealed record FailWebhookEventCommand(Guid WebhookEventId, string Reason) : ICommand<Unit>;

public sealed class FailWebhookEventHandler(IWebhookEventRepository repository)
    : ICommandHandler<FailWebhookEventCommand, Unit>
{
    public async Task<Result<Unit>> Handle(
        FailWebhookEventCommand command,
        CancellationToken cancellationToken)
    {
        var webhookEvent = await repository.GetByIdAsync(command.WebhookEventId, cancellationToken);
        if (webhookEvent is null)
        {
            return Result.Success(Unit.Value);
        }

        if (webhookEvent.Status is WebhookStatus.Processed
            or WebhookStatus.Ignored
            or WebhookStatus.Failed
            or WebhookStatus.Conflict)
        {
            return Result.Success(Unit.Value);
        }

        webhookEvent.MarkFailed(command.Reason);
        return Result.Success(Unit.Value);
    }
}

public sealed record MarkWebhookConflictCommand(Guid WebhookEventId, string Reason) : ICommand<Unit>;

public sealed class MarkWebhookConflictHandler(IWebhookEventRepository repository)
    : ICommandHandler<MarkWebhookConflictCommand, Unit>
{
    public async Task<Result<Unit>> Handle(
        MarkWebhookConflictCommand command,
        CancellationToken cancellationToken)
    {
        var webhookEvent = await repository.GetByIdAsync(command.WebhookEventId, cancellationToken);
        if (webhookEvent is null)
        {
            return Result.Success(Unit.Value);
        }

        if (webhookEvent.Status is WebhookStatus.Processed
            or WebhookStatus.Ignored
            or WebhookStatus.Failed
            or WebhookStatus.Conflict)
        {
            return Result.Success(Unit.Value);
        }

        webhookEvent.MarkConflict(command.Reason);
        return Result.Success(Unit.Value);
    }
}
