using Microsoft.Extensions.Logging;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Identity.Infrastructure;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email queued to {Recipient} with subject {Subject}.",
            message.To,
            message.Subject);

        return Task.CompletedTask;
    }
}
