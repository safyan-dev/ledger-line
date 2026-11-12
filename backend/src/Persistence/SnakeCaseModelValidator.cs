using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PaymentReconciliation.Persistence;

public sealed partial class SnakeCaseModelValidator(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<SnakeCaseModelValidator> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var model = dbContext.Model;

        var invalidNames = new List<string>();

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName) && !SnakeCasePattern().IsMatch(tableName))
            {
                invalidNames.Add($"table {entityType.ClrType.Name} -> {tableName}");
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrWhiteSpace(columnName) && !SnakeCasePattern().IsMatch(columnName))
                {
                    invalidNames.Add($"column {entityType.ClrType.Name}.{property.Name} -> {columnName}");
                }
            }
        }

        if (invalidNames.Count == 0)
        {
            logger.LogDebug("Database identifiers validated as snake_case.");
            return Task.CompletedTask;
        }

        var message = $"Database identifiers must be snake_case: {string.Join("; ", invalidNames)}";

        if (environment.IsDevelopment())
        {
            throw new InvalidOperationException(message);
        }

        logger.LogWarning(message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCasePattern();
}
