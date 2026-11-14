namespace PaymentReconciliation.Modules.Support.Application;

public sealed record SupportTicketDto(
    Guid Id,
    string Subject,
    string Description,
    string Category,
    string Priority,
    string Status,
    Guid CreatedByUserId,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt);

public sealed record SupportTicketMessageDto(
    Guid Id,
    Guid AuthorUserId,
    string AuthorName,
    string AuthorRole,
    string Body,
    bool IsInternal,
    DateTimeOffset CreatedAt);

public sealed record SupportTicketDetailDto(
    SupportTicketDto Ticket,
    IReadOnlyList<SupportTicketMessageDto> Messages);

public sealed record CreateSupportTicketRequest(
    string Subject,
    string Description,
    string Category,
    string Priority);

public sealed record ReplySupportTicketRequest(string Body);

public sealed record ChangeSupportTicketStatusRequest(string Status);
