using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PaymentReconciliation.Tests.Infrastructure;

public sealed class LedgerlineAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("payment_reconciliation_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = ConnectionString,
                ["Database:SeedDemoData"] = "false",
                ["Jwt:Issuer"] = "ledgerline-tests",
                ["Jwt:Audience"] = "ledgerline-tests",
                ["Jwt:SigningKey"] = "test-signing-key-with-at-least-thirty-two-characters",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Webhooks:SigningSecret"] = "whsec_test_ledgerline",
                ["Webhooks:Providers:mock:SigningSecret"] = "whsec_test_ledgerline"
            });
        });
    }
}
