using PaymentReconciliation.Modules.Support.Domain;

namespace PaymentReconciliation.Modules.Support.Application;

internal static class SupportTicketMapping
{
    public static SupportTicketDto ToDto(this SupportTicket ticket)
        => new(
            ticket.Id,
            ticket.Subject,
            ticket.Description,
            ticket.Category.ToString(),
            ticket.Priority.ToString(),
            ticket.Status.ToString(),
            ticket.CreatedByUserId,
            ticket.CreatedByName,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ResolvedAt,
            ticket.ClosedAt);

    public static SupportTicketMessageDto ToDto(this SupportTicketMessage message)
        => new(
            message.Id,
            message.AuthorUserId,
            message.AuthorName,
            message.AuthorRole,
            message.Body,
            message.IsInternal,
            message.CreatedAt);
}

internal static class SupportEnumParsing
{
    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray());
        if (normalized.Length == 0)
        {
            return false;
        }

        var match = Enum.GetNames<TEnum>()
            .FirstOrDefault(name => string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase));

        return match is not null && Enum.TryParse(match, out result);
    }
}
