using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PaymentReconciliation.SharedKernel.Diagnostics;

public static class LedgerlineMetrics
{
    public const string MeterName = "Ledgerline";
    public const string ActivitySourceName = "Ledgerline";

    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    private static readonly double[] DurationBuckets =
    [
        0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10
    ];

    public static readonly Counter<long> PaymentsSuccessful = Meter.CreateCounter<long>(
        "ledgerline.payments.successful",
        description: "Provider payments recorded successfully.");

    public static readonly Counter<long> PaymentsFailed = Meter.CreateCounter<long>(
        "ledgerline.payments.failed",
        description: "Provider payments that failed.");

    public static readonly Counter<long> WebhooksReceived = Meter.CreateCounter<long>(
        "ledgerline.webhooks.received",
        description: "Webhook events accepted into the processing pipeline.");

    public static readonly Counter<long> WebhooksProcessed = Meter.CreateCounter<long>(
        "ledgerline.webhooks.processed",
        description: "Webhook events processed into payments.");

    public static readonly Counter<long> WebhooksIgnored = Meter.CreateCounter<long>(
        "ledgerline.webhooks.ignored",
        description: "Webhook events ignored because their type is not handled.");

    public static readonly Counter<long> WebhooksDuplicatesIgnored = Meter.CreateCounter<long>(
        "ledgerline.webhooks.duplicates_ignored",
        description: "Duplicate webhook deliveries discarded by idempotency.");

    public static readonly Counter<long> WebhooksProcessingFailures = Meter.CreateCounter<long>(
        "ledgerline.webhooks.processing_failures",
        description: "Webhook events that failed processing.");

    public static readonly Histogram<double> WebhookProcessingDuration = Meter.CreateHistogram<double>(
        "ledgerline.webhooks.processing.duration",
        unit: "s",
        description: "End-to-end time spent processing a claimed webhook event.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

    public static readonly Counter<long> ReconciliationMatched = Meter.CreateCounter<long>(
        "ledgerline.reconciliation.matched",
        description: "Reconciliation attempts that matched a payment to an order.");

    public static readonly Counter<long> ReconciliationUnmatched = Meter.CreateCounter<long>(
        "ledgerline.reconciliation.unmatched",
        description: "Reconciliation attempts that left a payment unmatched or ambiguous.");

    public static readonly Counter<long> ReconciliationAmountMismatches = Meter.CreateCounter<long>(
        "ledgerline.reconciliation.amount_mismatches",
        description: "Reconciliation attempts where the payment amount differs from the order.");

    public static readonly Histogram<double> ReconciliationProcessingDuration = Meter.CreateHistogram<double>(
        "ledgerline.reconciliation.processing.duration",
        unit: "s",
        description: "Time spent matching and persisting a reconciliation result.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

    public static KeyValuePair<string, object?> ProviderTag(string provider)
        => new("provider", provider);

    public static KeyValuePair<string, object?> OutcomeTag(string outcome)
        => new("outcome", outcome);
}
