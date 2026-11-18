using Microsoft.Extensions.Logging;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Identity.Infrastructure;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        // Avoid recipient logging
        logger.LogInformation("Email queued with subject {Subject}.", message.Subject);

        return Task.CompletedTask;
    }
}
