using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Identity.Infrastructure;

// Plain SMTP transport. It works with Mailpit locally (which only captures messages)
// and with a real relay (Brevo, SES, SMTP2GO) once credentials are configured.
public sealed class SmtpEmailSender(
    IConfiguration configuration,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:Smtp:Host"];
        var fromAddress = configuration["Email:FromAddress"] ?? "no-reply@ledgerline.local";
        var fromName = configuration["Email:FromName"] ?? "Ledgerline";

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogError("Email delivery is configured for SMTP but Email:Smtp:Host is empty.");
            return;
        }

        var port = configuration.GetValue("Email:Smtp:Port", 1025);
        var enableSsl = configuration.GetValue("Email:Smtp:UseStartTls", false);
        var timeoutSeconds = Math.Max(1, configuration.GetValue("Email:Smtp:TimeoutSeconds", 10));
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = timeoutSeconds * 1000
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = message.Subject,
            Body = message.Body
        };
        mail.To.Add(message.To);

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
            logger.LogInformation("Email sent to the configured SMTP server.");
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            // A failed email must never fail the surrounding business operation.
            // Recipient and body are deliberately not logged.
            logger.LogWarning(
                exception,
                "Email delivery failed via SMTP host {Host}:{Port}.",
                host,
                port);
        }
    }
}
