using System.Text.RegularExpressions;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Tests.Integration.Infrastructure;

public sealed class TestEmailSender : IEmailSender
{
    private static readonly object Gate = new();
    private static readonly List<EmailMessage> Messages = new();
    private static readonly Regex TokenPattern = new(
        @"token[^:]*:\s*(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        lock (Gate)
        {
            Messages.Add(message);
        }

        return Task.CompletedTask;
    }

    public static string? LatestToken(string email, string subjectContains)
    {
        lock (Gate)
        {
            var message = Messages
                .Where(candidate =>
                    string.Equals(candidate.To, email, StringComparison.OrdinalIgnoreCase)
                    && candidate.Subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase))
                .LastOrDefault();

            if (message is null)
            {
                return null;
            }

            var match = TokenPattern.Match(message.Body);
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Messages.Clear();
        }
    }
}
