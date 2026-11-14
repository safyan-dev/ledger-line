using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Support.Contracts.Events;
using PaymentReconciliation.SharedKernel.Events;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Auditing.Application.EventHandlers;

public sealed class SupportTicketCreatedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SupportTicketCreated>
{
    public Task<Result> HandleAsync(SupportTicketCreated domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "support.ticket_created",
            "support",
            "SupportTicket",
            domainEvent.TicketId,
            $"Support ticket created: {domainEvent.Subject}.",
            new { domainEvent.Subject, domainEvent.Category, domainEvent.Priority },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SupportTicketRepliedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SupportTicketReplied>
{
    public Task<Result> HandleAsync(SupportTicketReplied domainEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "support.ticket_replied",
            "support",
            "SupportTicket",
            domainEvent.TicketId,
            "Support ticket received a reply.",
            new { domainEvent.MessageId },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}

public sealed class SupportTicketStatusChangedAuditHandler(IAuditLogRepository repository)
    : IDomainEventHandler<SupportTicketStatusChanged>
{
    public Task<Result> HandleAsync(
        SupportTicketStatusChanged domainEvent,
        CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            domainEvent.Actor,
            "support.ticket_status_changed",
            "support",
            "SupportTicket",
            domainEvent.TicketId,
            $"Support ticket status changed from {domainEvent.PreviousStatus} to {domainEvent.NewStatus}.",
            new { domainEvent.PreviousStatus, domainEvent.NewStatus },
            tenantId: domainEvent.TenantId));

        return Task.FromResult(Result.Success());
    }
}
