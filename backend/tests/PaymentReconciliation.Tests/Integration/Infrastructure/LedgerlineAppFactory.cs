using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentReconciliation.Modules.Identity.Domain;
using PaymentReconciliation.Persistence;
using PaymentReconciliation.SharedKernel.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace PaymentReconciliation.Tests.Integration.Infrastructure;

public sealed class LedgerlineAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly Dictionary<string, string?> _originalEnvironment = new();
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("payment_reconciliation_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task<(string Email, string Password)> CreatePlatformAdminAsync()
    {
        const string password = "PlatformPass123!";
        var email = $"platform-{Guid.NewGuid():N}@example.test";

        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Platform Admin",
            IsPlatformAdmin = true,
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the platform admin: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }

        return (email, password);
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        TestEmailSender.Clear();
        SetTestEnvironment("ConnectionStrings__Database", ConnectionString);
        SetTestEnvironment("Database__SeedDemoData", "false");
        SetTestEnvironment("Jwt__Issuer", "ledgerline-tests");
        SetTestEnvironment("Jwt__Audience", "ledgerline-tests");
        SetTestEnvironment("Jwt__SigningKey", "test-signing-key-with-at-least-thirty-two-characters");
        SetTestEnvironment("Jwt__AccessTokenMinutes", "15");
        SetTestEnvironment("Jwt__RefreshTokenDays", "7");
        SetTestEnvironment("Webhooks__SigningSecret", "whsec_test_ledgerline");
        SetTestEnvironment("Webhooks__Providers__mock__SigningSecret", "whsec_test_ledgerline");
        SetTestEnvironment("RateLimiting__GlobalPermitLimit", "100000");
        SetTestEnvironment("RateLimiting__GlobalWindowSeconds", "60");
        SetTestEnvironment("RateLimiting__RegistrationPermitLimit", "1000");
        SetTestEnvironment("RateLimiting__RegistrationWindowSeconds", "3600");
        SetTestEnvironment("RateLimiting__AuthPermitLimit", "1000");
        SetTestEnvironment("RateLimiting__AuthWindowSeconds", "60");

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();

        foreach (var (key, value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private void SetTestEnvironment(string key, string value)
    {
        _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
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
                ["Webhooks:Providers:mock:SigningSecret"] = "whsec_test_ledgerline",
                ["RateLimiting:AuthPermitLimit"] = "1000",
                ["RateLimiting:AuthWindowSeconds"] = "60"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, TestEmailSender>();
            services.RemoveAll<IWebhookSecretStore>();
            services.AddSingleton<IWebhookSecretStore, TestWebhookSecretStore>();
        });
    }
}
