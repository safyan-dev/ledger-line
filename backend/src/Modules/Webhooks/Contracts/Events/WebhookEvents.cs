using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Webhooks.Contracts.Events;

public sealed record WebhookEventReceived(
    Guid WebhookEventId,
    string Provider,
    string ProviderEventId,
    string EventType) : DomainEvent;

public sealed record WebhookEventProcessed(
    Guid WebhookEventId,
    string Provider,
    string ProviderEventId,
    Guid PaymentId) : DomainEvent;

public sealed record WebhookEventFailed(
    Guid WebhookEventId,
    string Provider,
    string ProviderEventId,
    string Reason) : DomainEvent;

public sealed record WebhookEventIgnored(
    Guid WebhookEventId,
    string Provider,
    string ProviderEventId,
    string Reason) : DomainEvent;

public sealed record WebhookEventConflict(
    Guid WebhookEventId,
    string Provider,
    string ProviderEventId,
    string Reason) : DomainEvent;
