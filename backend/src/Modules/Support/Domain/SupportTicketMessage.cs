using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Modules.Support.Domain;

public sealed class SupportTicketMessage : ITenantOwned
{
    private SupportTicketMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string AuthorName { get; private set; } = string.Empty;

    public string AuthorRole { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public bool IsInternal { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static SupportTicketMessage Create(
        Guid tenantId,
        Guid ticketId,
        Guid authorUserId,
        string authorName,
        string authorRole,
        string body,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || ticketId == Guid.Empty)
        {
            throw new DomainException("A ticket message requires a tenant and ticket.");
        }

        return new SupportTicketMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            AuthorName = authorName,
            AuthorRole = authorRole,
            Body = body,
            IsInternal = false,
            CreatedAt = now
        };
    }
}
