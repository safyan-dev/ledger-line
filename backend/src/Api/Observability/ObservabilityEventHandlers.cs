using PaymentReconciliation.Modules.Payments.Contracts.Events;
using PaymentReconciliation.Modules.Reconciliation.Contracts.Events;
using PaymentReconciliation.Modules.Webhooks.Contracts.Events;
using PaymentReconciliation.SharedKernel.Diagnostics;
using PaymentReconciliation.SharedKernel.Events;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Api.Observability;

public sealed class PaymentRecordedMetricsHandler : IDomainEventHandler<PaymentRecorded>
{
    public Task<Result> HandleAsync(PaymentRecorded domainEvent, CancellationToken cancellationToken)
    {
        LedgerlineMetrics.PaymentsSuccessful.Add(1, LedgerlineMetrics.ProviderTag(domainEvent.Provider));
        return Task.FromResult(Result.Success());
    }
}

public sealed class PaymentFailedMetricsHandler : IDomainEventHandler<PaymentFailed>
{
    public Task<Result> HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken)
    {
        LedgerlineMetrics.PaymentsFailed.Add(1);
        return Task.FromResult(Result.Success());
    }
}

public sealed class WebhookProcessedMetricsHandler : IDomainEventHandler<WebhookEventProcessed>
{
    public Task<Result> HandleAsync(WebhookEventProcessed domainEvent, CancellationToken cancellationToken)
    {
        LedgerlineMetrics.WebhooksProcessed.Add(1, LedgerlineMetrics.ProviderTag(domainEvent.Provider));
        return Task.FromResult(Result.Success());
    }
}

public sealed class WebhookFailedMetricsHandler : IDomainEventHandler<WebhookEventFailed>
{
    public Task<Result> HandleAsync(WebhookEventFailed domainEvent, CancellationToken cancellationToken)
    {
        LedgerlineMetrics.WebhooksProcessingFailures.Add(1, LedgerlineMetrics.ProviderTag(domainEvent.Provider));
        return Task.FromResult(Result.Success());
    }
}

public sealed class WebhookIgnoredMetricsHandler : IDomainEventHandler<WebhookEventIgnored>
{
    public Task<Result> HandleAsync(WebhookEventIgnored domainEvent, CancellationToken cancellationToken)
    {
        LedgerlineMetrics.WebhooksIgnored.Add(1, LedgerlineMetrics.ProviderTag(domainEvent.Provider));
        return Task.FromResult(Result.Success());
    }
}

public sealed class ReconciliationMetricsHandler : IDomainEventHandler<ReconciliationCompleted>
{
    public Task<Result> HandleAsync(ReconciliationCompleted domainEvent, CancellationToken cancellationToken)
    {
        var outcomeTag = LedgerlineMetrics.OutcomeTag(domainEvent.Outcome);

        switch (domainEvent.Outcome)
        {
            case "Matched":
            case "PartiallyMatched":
            case "ManuallyMatched":
                LedgerlineMetrics.ReconciliationMatched.Add(1, outcomeTag);
                break;
            case "Unmatched":
            case "Ambiguous":
                LedgerlineMetrics.ReconciliationUnmatched.Add(1, outcomeTag);
                break;
            case "AmountMismatch":
                LedgerlineMetrics.ReconciliationAmountMismatches.Add(1, outcomeTag);
                break;
        }

        return Task.FromResult(Result.Success());
    }
}
