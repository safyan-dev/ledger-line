namespace PaymentReconciliation.Modules.Webhooks.Infrastructure;

using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Webhooks.Contracts;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence.Abstractions;

public sealed class WebhookReporting(IAppDbContext dbContext) : IWebhookReporting
{
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => dbContext.Set<WebhookEvent>().AsNoTracking().CountAsync(cancellationToken);
}
