using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Webhooks.Application;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.Persistence;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.Tests.Integration.Infrastructure;
using Xunit;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class WebhookOutboxTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Due_event_is_claimed_once_and_can_be_rescheduled()
    {
        var client = factory.CreateClient();
        var session = await client.RegisterTenantAsync(NewSlug("outbox"));
        var receivedAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var eventId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(
                new TenantContextSnapshot(session.TenantId, session.TenantSlug, session.UserId, "Owner", false));
            dbContext.Set<WebhookEvent>().Add(WebhookEvent.Create(
                "mock",
                $"evt_outbox_{eventId:N}",
                "payment.succeeded",
                "{\"id\":\"evt_outbox\",\"type\":\"payment.succeeded\",\"data\":{}}",
                null,
                receivedAt,
                tenantId: session.TenantId));
            await dbContext.SaveChangesAsync();
        }

        var claimAt = receivedAt.AddMinutes(1);
        IReadOnlyList<WebhookOutboxItem>[] claims;

        using (var firstScope = factory.Services.CreateScope())
        using (var secondScope = factory.Services.CreateScope())
        {
            var firstOutbox = firstScope.ServiceProvider.GetRequiredService<IWebhookOutbox>();
            var secondOutbox = secondScope.ServiceProvider.GetRequiredService<IWebhookOutbox>();
            claims = await Task.WhenAll(
                firstOutbox.ClaimDueAsync(10, claimAt),
                secondOutbox.ClaimDueAsync(10, claimAt));
        }

        Assert.Equal(1, claims.Sum(claim => claim.Count));
        var claimed = claims.SelectMany(claim => claim).Single();
        Assert.Equal(session.TenantId, claimed.TenantId);

        using (var scope = factory.Services.CreateScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IWebhookOutbox>();
            await outbox.ScheduleRetryAsync(
                claimed.WebhookEventId,
                "transient_failure",
                claimAt.AddSeconds(5));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await dbContext.Set<WebhookEvent>()
                .IgnoreQueryFilters()
                .SingleAsync(webhookEvent => webhookEvent.Id == claimed.WebhookEventId);
            Assert.Equal(WebhookStatus.Received, stored.Status);
            Assert.Equal("transient_failure", stored.FailureReason);
            Assert.Equal(2, stored.AttemptCount);
            Assert.Equal(ToDatabasePrecision(claimAt.AddSeconds(5)), stored.NextAttemptAt);
        }
    }

    // PostgreSQL timestamptz stores microseconds; a .NET tick is 100 ns, so a
    // sub-microsecond remainder is truncated on round-trip. Compare at the
    // precision the column actually keeps.
    private static DateTimeOffset ToDatabasePrecision(DateTimeOffset value)
        => new(value.Ticks - (value.Ticks % 10), value.Offset);

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}
