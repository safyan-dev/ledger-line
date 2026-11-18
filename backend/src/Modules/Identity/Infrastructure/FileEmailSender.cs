using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Identity.Infrastructure;

public sealed class FileEmailSender(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<FileEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var configuredDirectory = configuration["Email:OutboxDirectory"];
        var directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(environment.ContentRootPath, "email-outbox")
            : Path.IsPathRooted(configuredDirectory)
                ? configuredDirectory
                : Path.Combine(environment.ContentRootPath, configuredDirectory);

        Directory.CreateDirectory(directory);

        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.txt";
        var content = $"To: {message.To}{Environment.NewLine}"
                      + $"Subject: {message.Subject}{Environment.NewLine}{Environment.NewLine}"
                      + $"{message.Body}{Environment.NewLine}";

        await File.WriteAllTextAsync(
            Path.Combine(directory, fileName),
            content,
            cancellationToken);

        // Avoid recipient logging
        logger.LogInformation("Email written to the development outbox.");
    }
}
