using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.Modules.Webhooks.Contracts;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Modules.Webhooks.Infrastructure;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Webhooks;

public static class WebhooksModule
{
    public static IServiceCollection AddWebhooksModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var hasSigningSecret = !string.IsNullOrWhiteSpace(configuration["Webhooks:SigningSecret"])
                               || configuration.GetSection("Webhooks:Providers")
                                   .GetChildren()
                                   .Any(provider => !string.IsNullOrWhiteSpace(provider["SigningSecret"]));
        if (!hasSigningSecret)
        {
            throw new InvalidOperationException(
                "No webhook signing secret is configured. Run backend/tools/setup-dev-secrets.sh (or .ps1) or set Webhooks__SigningSecret.");
        }

        services.AddCqrsHandlers(typeof(WebhookEvent).Assembly);
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.AddScoped<IWebhookSignatureVerifier, HmacWebhookSignatureVerifier>();
        services.AddSingleton<IWebhookPayloadParser, JsonWebhookPayloadParser>();
        services.AddScoped<IWebhookReporting, WebhookReporting>();

        services.Configure<WebhookIngestionOptions>(configuration.GetSection("Webhooks:Ingestion"));
        services.AddSingleton<IWebhookIngressQueue, WebhookIngressQueue>();
        services.AddScoped<IWebhookEventClaimStore, WebhookEventClaimStore>();
        services.AddHostedService<WebhookIngressWorker>();
        services.AddHostedService<WebhookRecoveryWorker>();

        return services;
    }
}
