using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed class WebhookSecretService(
    IAppDbContext dbContext,
    IUnitOfWork unitOfWork,
    IDataProtectionProvider dataProtectionProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<WebhookSecretService> logger) : IWebhookSecretStore
{
    private static readonly string[] Providers = ["mock", "stripe"];
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Ledgerline.WebhookSecrets");

    public async Task<string?> GetSecretAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var stored = await dbContext.Set<TenantWebhookSecret>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                secret => secret.TenantId == tenantId
                          && secret.Provider == provider.Trim().ToLowerInvariant(),
                cancellationToken);

        if (stored is not null)
        {
            try
            {
                return _protector.Unprotect(stored.ProtectedSecret);
            }
            catch (CryptographicException)
            {
                logger.LogWarning(
                    "Webhook secret decryption failed for tenant {TenantId} and provider {Provider}.",
                    tenantId,
                    provider);
                return null;
            }
        }

        if (!environment.IsDevelopment())
        {
            return null;
        }

        return configuration[$"Webhooks:Providers:{provider}:SigningSecret"]
               ?? configuration["Webhooks:SigningSecret"];
    }

    public async Task EnsureSecretsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Set<TenantWebhookSecret>()
            .IgnoreQueryFilters()
            .Where(secret => secret.TenantId == tenantId)
            .Select(secret => secret.Provider)
            .ToListAsync(cancellationToken);
        var existingProviders = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in Providers)
        {
            if (existingProviders.Contains(provider))
            {
                continue;
            }

            dbContext.Set<TenantWebhookSecret>().Add(TenantWebhookSecret.Create(
                tenantId,
                provider,
                _protector.Protect(GenerateSecret()),
                DateTimeOffset.UtcNow));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> RotateSecretAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = provider.Trim().ToLowerInvariant();
        if (!Providers.Contains(normalizedProvider, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Unsupported webhook provider.", nameof(provider));
        }

        var stored = await dbContext.Set<TenantWebhookSecret>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                secret => secret.TenantId == tenantId && secret.Provider == normalizedProvider,
                cancellationToken);

        var secret = GenerateSecret();
        var protectedSecret = _protector.Protect(secret);

        if (stored is null)
        {
            dbContext.Set<TenantWebhookSecret>().Add(TenantWebhookSecret.Create(
                tenantId,
                normalizedProvider,
                protectedSecret,
                DateTimeOffset.UtcNow));
        }
        else
        {
            stored.Rotate(protectedSecret, DateTimeOffset.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return secret;
    }

    private static string GenerateSecret()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
}
