using PaymentReconciliation.Modules.Support.Contracts.Events;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Support.Domain;

public enum SupportTicketCategory
{
    TechnicalIssue,
    PaymentIssue,
    ReconciliationIssue,
    AccountIssue,
    BillingIssue,
    Other
}

public enum SupportTicketPriority
{
    Low,
    Normal,
    High
}

public enum SupportTicketStatus
{
    Open,
    InProgress,
    WaitingForCustomer,
    Resolved,
    Closed
}

public static class SupportTicketWorkflow
{
    public static bool CanTransition(SupportTicketStatus from, SupportTicketStatus to)
        => (from, to) switch
        {
            (SupportTicketStatus.Open, SupportTicketStatus.InProgress
                or SupportTicketStatus.WaitingForCustomer
                or SupportTicketStatus.Resolved) => true,
            (SupportTicketStatus.InProgress, SupportTicketStatus.WaitingForCustomer
                or SupportTicketStatus.Resolved) => true,
            (SupportTicketStatus.WaitingForCustomer, SupportTicketStatus.InProgress
                or SupportTicketStatus.Resolved) => true,
            (SupportTicketStatus.Resolved, SupportTicketStatus.Closed
                or SupportTicketStatus.Open) => true,
            (SupportTicketStatus.Closed, SupportTicketStatus.Open) => true,
            _ => false
        };
}

public sealed class SupportTicket : AggregateRoot, ITenantOwned
{
    private SupportTicket()
    {
    }

    public Guid TenantId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public SupportTicketCategory Category { get; private set; }

    public SupportTicketPriority Priority { get; private set; }

    public SupportTicketStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string CreatedByName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public static SupportTicket Create(
        Guid tenantId,
        string subject,
        string description,
        SupportTicketCategory category,
        SupportTicketPriority priority,
        Guid createdByUserId,
        string createdByName,
        DateTimeOffset now,
        AuditActor actor)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Support tickets require a tenant.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainException("Support tickets require a creator.");
        }

        var trimmedSubject = subject?.Trim() ?? string.Empty;
        if (trimmedSubject.Length is 0 or > 200)
        {
            throw new DomainException("Ticket subject must be between 1 and 200 characters.");
        }

        var trimmedDescription = description?.Trim() ?? string.Empty;
        if (trimmedDescription.Length is 0 or > 4000)
        {
            throw new DomainException("Ticket description must be between 1 and 4000 characters.");
        }

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Subject = trimmedSubject,
            Description = trimmedDescription,
            Category = category,
            Priority = priority,
            Status = SupportTicketStatus.Open,
            CreatedByUserId = createdByUserId,
            CreatedByName = createdByName,
            CreatedAt = now,
            UpdatedAt = now
        };

        ticket.Raise(new SupportTicketCreated(
            tenantId,
            ticket.Id,
            ticket.Subject,
            category.ToString(),
            priority.ToString(),
            actor));

        return ticket;
    }

    public SupportTicketMessage AddMessage(
        Guid authorUserId,
        string authorName,
        string authorRole,
        string body,
        DateTimeOffset now,
        AuditActor actor)
    {
        if (Status == SupportTicketStatus.Closed)
        {
            throw new ConflictException("ticket.closed", "A closed ticket cannot receive replies.");
        }

        var trimmedBody = body?.Trim() ?? string.Empty;
        if (trimmedBody.Length is 0 or > 4000)
        {
            throw new DomainException("Reply body must be between 1 and 4000 characters.");
        }

        var message = SupportTicketMessage.Create(
            TenantId,
            Id,
            authorUserId,
            authorName,
            authorRole,
            trimmedBody,
            now);

        UpdatedAt = now;
        Raise(new SupportTicketReplied(TenantId, Id, message.Id, actor));

        return message;
    }

    public void ChangeStatus(SupportTicketStatus status, DateTimeOffset now, AuditActor actor)
    {
        if (Status == status)
        {
            return;
        }

        if (!SupportTicketWorkflow.CanTransition(Status, status))
        {
            throw new ConflictException(
                "ticket.invalid_transition",
                $"A ticket cannot transition from {Status} to {status}.");
        }

        var previousStatus = Status;
        Status = status;
        UpdatedAt = now;

        switch (status)
        {
            case SupportTicketStatus.Resolved:
                ResolvedAt = now;
                ClosedAt = null;
                break;
            case SupportTicketStatus.Closed:
                ClosedAt = now;
                break;
            case SupportTicketStatus.Open:
            case SupportTicketStatus.InProgress:
            case SupportTicketStatus.WaitingForCustomer:
                ResolvedAt = null;
                ClosedAt = null;
                break;
        }

        Raise(new SupportTicketStatusChanged(
            TenantId,
            Id,
            previousStatus.ToString(),
            status.ToString(),
            actor));
    }
}
