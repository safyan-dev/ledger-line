using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence.Abstractions;

namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

public sealed class WebhookEventRepository(IAppDbContext dbContext) : IWebhookEventRepository
{
    public Task<WebhookEvent?> FindByProviderEventIdAsync(
        string provider,
        string providerEventId,
        CancellationToken cancellationToken = default)
        => dbContext.Set<WebhookEvent>().FirstOrDefaultAsync(
            webhookEvent => webhookEvent.Provider == provider && webhookEvent.ProviderEventId == providerEventId,
            cancellationToken);

    public Task<WebhookEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Set<WebhookEvent>().FirstOrDefaultAsync(webhookEvent => webhookEvent.Id == id, cancellationToken);

    public void Add(WebhookEvent webhookEvent)
        => dbContext.Set<WebhookEvent>().Add(webhookEvent);
}
