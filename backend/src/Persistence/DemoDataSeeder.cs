using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Identity.Domain;
using PaymentReconciliation.Modules.Orders.Domain;
using PaymentReconciliation.Modules.Payments.Domain;
using PaymentReconciliation.Modules.Reconciliation.Domain;
using PaymentReconciliation.Modules.Tenancy;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Modules.Webhooks.Domain;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;

namespace PaymentReconciliation.Persistence;

public sealed class DemoDataSeeder(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration)
{
    private static readonly Guid AveryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SystemActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var demoTenant = await EnsureDemoTenantAsync(cancellationToken);
        dbContext.TenantContext.Set(new TenantContextSnapshot(
            demoTenant.Id,
            demoTenant.Slug,
            AveryId,
            TenantRole.Owner.ToString(),
            false));

        if (await dbContext.Set<Order>().AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var tenantId = demoTenant.Id;
        var averyActor = new AuditActor(AveryId, "Avery Chen", "Operations");
        var systemActor = AuditActor.System;

        dbContext.Set<Actor>().AddRange(
            Actor.Create(AveryId, "Avery Chen", "avery.chen@ledgerline.local", "Operations", false),
            Actor.Create(SystemActorId, "system", "system@ledgerline.local", "System", true));

        var northwind = Order.Create(
            "ORD-2026-0001", "Northwind Traders", "NW-5512", "Q4 platform licence",
            new Money(1250m, "USD"), averyActor, DaysAgo(now, 12), tenantId);

        var acme = Order.Create(
            "ORD-2026-0002", "Acme Industries", "ACME-77", "Annual support renewal",
            new Money(899.99m, "USD"), averyActor, DaysAgo(now, 9), tenantId);

        var globex = Order.Create(
            "ORD-2026-0003", "Globex Corporation", "GLX-14", "Hardware maintenance",
            new Money(320m, "USD"), averyActor, DaysAgo(now, 6), tenantId);

        var initech = Order.Create(
            "ORD-2026-0004", "Initech", "INI-02", "Consulting hours",
            new Money(74.50m, "USD"), averyActor, DaysAgo(now, 4), tenantId);

        var umbrella = Order.Create(
            "ORD-2026-0005", "Umbrella Health", "UMB-91", "Compliance audit package",
            new Money(2400m, "USD"), averyActor, DaysAgo(now, 3), tenantId);

        var soylent = Order.Create(
            "ORD-2026-0006", "Soylent Foods", "SOY-33", "Pilot subscription",
            new Money(615.25m, "USD"), averyActor, DaysAgo(now, 2), tenantId);

        soylent.Cancel("Customer withdrew the purchase order.", averyActor);

        dbContext.Set<Order>().AddRange(northwind, acme, globex, initech, umbrella, soylent);

        Payment AddPayment(
            string provider,
            string providerPaymentId,
            decimal amount,
            string? customerName,
            string? customerReference,
            string? reference,
            DateTimeOffset receivedAt,
            Order? matchedOrder,
            ReconciliationOutcome outcome,
            MatchStrategy strategy,
            string? reason)
        {
            var payment = Payment.Create(
                provider,
                providerPaymentId,
                new Money(amount, "USD"),
                customerName,
                customerReference,
                reference,
                receivedAt,
                matchedOrder is null ? systemActor : averyActor,
                receivedAt,
                tenantId: tenantId);

            if (matchedOrder is not null)
            {
                matchedOrder.AllocatePayment(payment.Id, new Money(amount, "USD"), averyActor);
                payment.MarkMatched(matchedOrder.Id, averyActor);
            }
            else
            {
                payment.MarkUnmatched(reason ?? "No matching order was found.", systemActor);
            }

            dbContext.Set<Payment>().Add(payment);

            dbContext.Set<ReconciliationRecord>().Add(ReconciliationRecord.Create(
                payment.Id,
                matchedOrder?.Id,
                outcome,
                strategy,
                matchedOrder?.ExpectedAmount,
                amount,
                "USD",
                reason,
                null,
                matchedOrder is null ? systemActor : averyActor,
                receivedAt.AddMinutes(2),
                tenantId: tenantId));

            return payment;
        }

        var p1 = AddPayment("mock", "pay_mock_1001", 500m, "Northwind Traders", "NW-5512", "ORD-2026-0001",
            DaysAgo(now, 12, 10), northwind, ReconciliationOutcome.PartiallyMatched, MatchStrategy.ExactReference,
            "Order number reference matched.");

        var p2 = AddPayment("mock", "pay_mock_1002", 750m, "Northwind Traders", "NW-5512", "ORD-2026-0001",
            DaysAgo(now, 11, 15), northwind, ReconciliationOutcome.Matched, MatchStrategy.ExactReference,
            "Order number reference matched.");

        var p3 = AddPayment("stripe", "pay_stripe_2001", 400m, "Acme Industries", "ACME-77", "ORD-2026-0002",
            DaysAgo(now, 9, 11), acme, ReconciliationOutcome.PartiallyMatched, MatchStrategy.ExactReference,
            "Order number reference matched.");

        var p4 = AddPayment("mock", "pay_mock_1004", 100m, "Initech", "INI-02", "ORD-2026-0004",
            DaysAgo(now, 4, 16), initech, ReconciliationOutcome.Overpaid, MatchStrategy.ExactReference,
            "Order number reference matched.");

        var p5 = AddPayment("stripe", "pay_stripe_2002", 2400m, "Umbrella Health", "UMB-91", "ORD-2026-0005",
            DaysAgo(now, 3, 9), umbrella, ReconciliationOutcome.Matched, MatchStrategy.ExactReference,
            "Order number reference matched.");

        var p6 = AddPayment("mock", "pay_mock_1006", 320m, "Globex Corporation", "GLX-14", null,
            DaysAgo(now, 6, 14), globex, ReconciliationOutcome.Matched, MatchStrategy.CustomerAndAmount,
            "Customer reference and amount matched.");

        var p7 = AddPayment("mock", "pay_mock_1007", 118.20m, "Cascade Freight", null, null,
            DaysAgo(now, 5, 12), null, ReconciliationOutcome.Unmatched, MatchStrategy.None,
            "No matching order was found.");

        var p8 = AddPayment("stripe", "pay_stripe_2008", 42m, "Bluebird Studio", null, null,
            DaysAgo(now, 2, 10), null, ReconciliationOutcome.Unmatched, MatchStrategy.None,
            "No matching order was found.");

        var p9 = AddPayment("mock", "pay_mock_1009", 615.25m, "Soylent Foods", "SOY-33", "ORD-2026-0006",
            DaysAgo(now, 2, 13), null, ReconciliationOutcome.Unmatched, MatchStrategy.None,
            "The referenced order is cancelled and cannot receive payments.");

        var p10 = AddPayment("stripe", "pay_stripe_2010", 96.75m, "Acme Industries", "ACME-77", null,
            DaysAgo(now, 4, 15), null, ReconciliationOutcome.Unmatched, MatchStrategy.None,
            "Customer matched but the amount did not match any open order.");

        var p11 = AddPayment("mock", "pay_mock_1011", 88.10m, "Harbor Logistics", null, null,
            DaysAgo(now, 1, 9), null, ReconciliationOutcome.Unmatched, MatchStrategy.None,
            "No matching order was found.");

        AddPayment("stripe", "pay_stripe_2012", 250m, "Vertex Media", null, null,
            DaysAgo(now, 1, 12), null, ReconciliationOutcome.Unmatched, MatchStrategy.None,
            "No matching order was found.");

        AddPayment("mock", "pay_mock_1013", 19.99m, "Nimbus Retail", null, null,
            DaysAgo(now, 0, 8), null, ReconciliationOutcome.Unmatched, MatchStrategy.None,
            "No matching order was found.");

        var p14 = AddPayment("manual", "manual_9f4a2c", 120m, "Contoso Ltd", "CON-18", null,
            DaysAgo(now, 0, 10), null, ReconciliationOutcome.Unmatched, MatchStrategy.None,
            "No matching order was found.");

        acme.AllocatePayment(p10.Id, new Money(96.75m, "USD"), averyActor);
        p10.MarkMatched(acme.Id, averyActor);

        dbContext.Set<ReconciliationRecord>().Add(ReconciliationRecord.Create(
            p10.Id,
            acme.Id,
            ReconciliationOutcome.ManuallyMatched,
            MatchStrategy.Manual,
            acme.ExpectedAmount,
            96.75m,
            "USD",
            "Matched manually by an operator.",
            "Confirmed against the customer remittance advice.",
            averyActor,
            DaysAgo(now, 3, 17)));

        var processedPayload =
            """{"id":"%ID%","type":"payment.succeeded","created":1762400000,"data":{"paymentId":"%PAYMENT%","amount":50000,"currency":"USD","reference":"ORD-2026-0001","customerName":"Northwind Traders","customerReference":"NW-5512"}}""";

        WebhookEvent AddWebhook(
            string providerEventId,
            string eventType,
            string payload,
            WebhookStatus status,
            DateTimeOffset receivedAt,
            Guid? paymentId = null,
            string? failureReason = null)
        {
            var webhookEvent = WebhookEvent.Create(
                providerEventId.Contains("stripe", StringComparison.Ordinal) ? "stripe" : "mock",
                providerEventId,
                eventType,
                payload,
                $"sha256=seed-{providerEventId}",
                receivedAt,
                tenantId: tenantId);

            switch (status)
            {
                case WebhookStatus.Processed when paymentId is not null:
                    webhookEvent.MarkProcessed(paymentId.Value, receivedAt.AddSeconds(1));
                    break;
                case WebhookStatus.Failed:
                    webhookEvent.MarkFailed(failureReason ?? "Processing failed.", receivedAt.AddSeconds(1));
                    break;
                case WebhookStatus.Ignored:
                    webhookEvent.MarkIgnored(failureReason ?? "Event type is not handled.", receivedAt.AddSeconds(1));
                    break;
            }

            dbContext.Set<WebhookEvent>().Add(webhookEvent);
            return webhookEvent;
        }

        var w1 = AddWebhook("evt_mock_1001", "payment.succeeded", processedPayload.Replace("%ID%", "evt_mock_1001").Replace("%PAYMENT%", "pay_mock_1001"), WebhookStatus.Processed, DaysAgo(now, 12, 10), p1.Id);
        var w2 = AddWebhook("evt_mock_1002", "payment.succeeded", processedPayload.Replace("%ID%", "evt_mock_1002").Replace("%PAYMENT%", "pay_mock_1002"), WebhookStatus.Processed, DaysAgo(now, 11, 15), p2.Id);
        var w3 = AddWebhook("evt_stripe_2001", "payment.succeeded", processedPayload.Replace("%ID%", "evt_stripe_2001").Replace("%PAYMENT%", "pay_stripe_2001"), WebhookStatus.Processed, DaysAgo(now, 9, 11), p3.Id);
        var w4 = AddWebhook("evt_mock_1004", "payment.succeeded", processedPayload.Replace("%ID%", "evt_mock_1004").Replace("%PAYMENT%", "pay_mock_1004"), WebhookStatus.Processed, DaysAgo(now, 4, 16), p4.Id);
        var w5 = AddWebhook("evt_stripe_2002", "payment.succeeded", processedPayload.Replace("%ID%", "evt_stripe_2002").Replace("%PAYMENT%", "pay_stripe_2002"), WebhookStatus.Processed, DaysAgo(now, 3, 9), p5.Id);
        var w6 = AddWebhook("evt_mock_1006", "payment.succeeded", processedPayload.Replace("%ID%", "evt_mock_1006").Replace("%PAYMENT%", "pay_mock_1006"), WebhookStatus.Processed, DaysAgo(now, 6, 14), p6.Id);
        var w7 = AddWebhook("evt_mock_1007", "payment.succeeded", processedPayload.Replace("%ID%", "evt_mock_1007").Replace("%PAYMENT%", "pay_mock_1007"), WebhookStatus.Processed, DaysAgo(now, 5, 12), p7.Id);
        var w8 = AddWebhook("evt_stripe_2008", "payment.succeeded", processedPayload.Replace("%ID%", "evt_stripe_2008").Replace("%PAYMENT%", "pay_stripe_2008"), WebhookStatus.Processed, DaysAgo(now, 2, 10), p8.Id);
        var w9 = AddWebhook("evt_mock_1009", "payment.succeeded", processedPayload.Replace("%ID%", "evt_mock_1009").Replace("%PAYMENT%", "pay_mock_1009"), WebhookStatus.Processed, DaysAgo(now, 2, 13), p9.Id);
        var w10 = AddWebhook("evt_mock_1011", "payment.succeeded", processedPayload.Replace("%ID%", "evt_mock_1011").Replace("%PAYMENT%", "pay_mock_1011"), WebhookStatus.Failed, DaysAgo(now, 1, 9), null, "Signature verification failed.");
        var w11 = AddWebhook("evt_mock_1012", "customer.created", """{"id":"evt_mock_1012","type":"customer.created","data":{"customerId":"cus_88"}}""", WebhookStatus.Ignored, DaysAgo(now, 1, 10), null, "Event type is not handled.");
        var w12 = AddWebhook("evt_mock_1013", "payment.succeeded", processedPayload.Replace("%ID%", "evt_mock_1013").Replace("%PAYMENT%", "pay_mock_1013"), WebhookStatus.Processed, DaysAgo(now, 0, 8), p11.Id);

        dbContext.Set<AuditLog>().AddRange(
            AuditLog.Create(averyActor, "order.created", "orders", "Order", northwind.Id,
                "Order ORD-2026-0001 created for Northwind Traders (USD 1,250.00).", null, DaysAgo(now, 12), tenantId: tenantId),
            AuditLog.Create(averyActor, "order.created", "orders", "Order", acme.Id,
                "Order ORD-2026-0002 created for Acme Industries (USD 899.99).", null, DaysAgo(now, 9), tenantId: tenantId),
            AuditLog.Create(systemActor, "payment.recorded", "payments", "Payment", p3.Id,
                "Payment pay_stripe_2001 recorded from stripe (USD 400.00).", null, DaysAgo(now, 9, 11), tenantId: tenantId),
            AuditLog.Create(averyActor, "reconciliation.completed", "reconciliation", "ReconciliationRecord", p3.Id,
                "Reconciliation finished with outcome PartiallyMatched (USD 400.00).", null, DaysAgo(now, 9, 11), tenantId: tenantId),
            AuditLog.Create(averyActor, "order.cancelled", "orders", "Order", soylent.Id,
                "Order ORD-2026-0006 cancelled. Reason: Customer withdrew the purchase order.", null, DaysAgo(now, 2, 9), tenantId: tenantId),
            AuditLog.Create(systemActor, "reconciliation.unmatched", "reconciliation", "ReconciliationRecord", p9.Id,
                "Reconciliation finished with outcome Unmatched (USD 615.25).", null, DaysAgo(now, 2, 13), tenantId: tenantId),
            AuditLog.Create(averyActor, "reconciliation.manually_matched", "reconciliation", "ReconciliationRecord", p10.Id,
                "Reconciliation finished with outcome ManuallyMatched (USD 96.75).", null, DaysAgo(now, 3, 17), tenantId: tenantId),
            AuditLog.Create(systemActor, "webhook.failed", "webhooks", "WebhookEvent", w10.Id,
                "Webhook evt_mock_1011 from mock failed: Signature verification failed.", null, DaysAgo(now, 1, 9), tenantId: tenantId),
            AuditLog.Create(systemActor, "webhook.processed", "webhooks", "WebhookEvent", w12.Id,
                "Webhook evt_mock_1013 from mock processed into a payment.", null, DaysAgo(now, 0, 8), tenantId: tenantId),
            AuditLog.Create(systemActor, "payment.recorded", "payments", "Payment", p14.Id,
                "Payment manual_9f4a2c recorded from manual (USD 120.00).", null, DaysAgo(now, 0, 10), tenantId: tenantId));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Tenant> EnsureDemoTenantAsync(CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Set<Tenant>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Slug == TenantDefaults.DemoSlug, cancellationToken);

        if (tenant is null)
        {
            tenant = Tenant.Create(TenantDefaults.DemoName, TenantDefaults.DemoSlug);
            dbContext.Set<Tenant>().Add(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var user = await userManager.FindByEmailAsync("avery.chen@ledgerline.local");
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = AveryId,
                UserName = "avery.chen@ledgerline.local",
                Email = "avery.chen@ledgerline.local",
                EmailConfirmed = true,
                DisplayName = "Avery Chen",
                Status = UserStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var password = configuration["Demo:Password"] ?? "Demo123!ChangeMe";
            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var details = string.Join("; ", createResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Could not create the demo user: {details}");
            }
        }

        var membership = await dbContext.Set<TenantUser>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenant.Id && candidate.UserId == user.Id,
                cancellationToken);

        if (membership is null)
        {
            dbContext.Set<TenantUser>().Add(TenantUser.Create(tenant.Id, user.Id, TenantRole.Owner));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return tenant;
    }

    private static DateTimeOffset DaysAgo(DateTimeOffset now, int days, int hour = 9)
        => new(now.UtcDateTime.Date.AddDays(-days).AddHours(hour), TimeSpan.Zero);
}
