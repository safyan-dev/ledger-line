using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using PaymentReconciliation.Tests.Integration.Infrastructure;

namespace PaymentReconciliation.Tests.Integration;

[Collection(LedgerlineCollection.Name)]
public sealed class PaymentWorkflowIntegrationTests(LedgerlineAppFactory factory)
{
    [Fact]
    public async Task Successful_webhook_records_payment_and_marks_order_paid()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whsuccess"));
        var orderId = await client.CreateOrderAsync(owner, 100m);
        var eventId = WebhookTestClient.NewEventId();
        var providerPaymentId = WebhookTestClient.NewProviderPaymentId();

        var ack = await SendAsync(client, owner, eventId, providerPaymentId, 100m, orderId: orderId);
        Assert.Equal(HttpStatusCode.Accepted, ack.StatusCode);

        var webhook = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed");

        var paymentId = webhook.GetProperty("paymentId").GetGuid();
        var payment = await WebhookTestClient.GetPaymentAsync(client, owner, paymentId);
        Assert.Equal("Matched", payment.GetProperty("status").GetString());
        Assert.Equal(orderId, payment.GetProperty("orderId").GetGuid());
        Assert.Equal(100m, payment.GetProperty("amount").GetDecimal());

        var order = await WebhookTestClient.GetOrderAsync(client, owner, orderId);
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        Assert.Equal(100m, order.GetProperty("paidAmount").GetDecimal());

        var records = await WebhookTestClient.GetReconciliationAsync(client, owner, paymentId);
        var record = Assert.Single(records);
        Assert.Equal("Matched", record.GetProperty("outcome").GetString());
        Assert.Equal("OrderId", record.GetProperty("strategy").GetString());
        Assert.Equal(orderId, record.GetProperty("orderId").GetGuid());
    }

    [Fact]
    public async Task Duplicate_webhook_delivery_is_idempotent()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whduplicate"));
        var orderId = await client.CreateOrderAsync(owner, 100m);
        var eventId = WebhookTestClient.NewEventId();
        var providerPaymentId = WebhookTestClient.NewProviderPaymentId();
        var payload = WebhookTestClient.PaymentPayload(eventId, providerPaymentId, 100m, orderId: orderId);

        var first = await WebhookTestClient.SendSignedAsync(client, owner.TenantSlug, payload);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstEvent = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed");
        var paymentId = firstEvent.GetProperty("paymentId").GetGuid();

        var second = await WebhookTestClient.SendSignedAsync(client, owner.TenantSlug, payload);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        var duplicateEvent = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed"
                    && item.GetProperty("duplicateCount").GetInt32() >= 1);

        Assert.Equal(paymentId, duplicateEvent.GetProperty("paymentId").GetGuid());

        var payments = await WebhookTestClient.GetPaymentsAsync(client, owner, orderId: orderId);
        Assert.Equal(1, payments.GetProperty("totalCount").GetInt32());

        var order = await WebhookTestClient.GetOrderAsync(client, owner, orderId);
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        Assert.Equal(100m, order.GetProperty("paidAmount").GetDecimal());

        var records = await WebhookTestClient.GetReconciliationAsync(client, owner, paymentId);
        Assert.Single(records);
    }

    [Fact]
    public async Task Amount_mismatch_is_recorded_and_not_allocated()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whmismatch"));
        var orderId = await client.CreateOrderAsync(owner, 100m);
        var eventId = WebhookTestClient.NewEventId();
        var providerPaymentId = WebhookTestClient.NewProviderPaymentId();

        var ack = await SendAsync(client, owner, eventId, providerPaymentId, 90m, orderId: orderId);
        Assert.Equal(HttpStatusCode.Accepted, ack.StatusCode);

        var webhook = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed");
        var paymentId = webhook.GetProperty("paymentId").GetGuid();

        var payment = await WebhookTestClient.GetPaymentAsync(client, owner, paymentId);
        Assert.Equal("Unmatched", payment.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, payment.GetProperty("orderId").ValueKind);

        var order = await WebhookTestClient.GetOrderAsync(client, owner, orderId);
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.Equal(0m, order.GetProperty("paidAmount").GetDecimal());

        var records = await WebhookTestClient.GetReconciliationAsync(client, owner, paymentId);
        var record = Assert.Single(records);
        Assert.Equal("AmountMismatch", record.GetProperty("outcome").GetString());
        Assert.Equal(orderId, record.GetProperty("orderId").GetGuid());
        Assert.Equal(100m, record.GetProperty("expectedAmount").GetDecimal());
        Assert.Equal(90m, record.GetProperty("actualAmount").GetDecimal());
        Assert.Contains("Amount mismatch", record.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Payment_without_matching_order_is_unmatched()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whunmatched"));
        var eventId = WebhookTestClient.NewEventId();
        var providerPaymentId = WebhookTestClient.NewProviderPaymentId();

        var reference = $"MISSING-{Guid.NewGuid():N}";
        var ack = await SendAsync(client, owner, eventId, providerPaymentId, 100m, reference: reference);
        Assert.Equal(HttpStatusCode.Accepted, ack.StatusCode);

        var webhook = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed");
        var paymentId = webhook.GetProperty("paymentId").GetGuid();

        var payment = await WebhookTestClient.GetPaymentAsync(client, owner, paymentId);
        Assert.Equal("Unmatched", payment.GetProperty("status").GetString());
        Assert.Equal(reference, payment.GetProperty("reference").GetString());
        Assert.Equal(JsonValueKind.Null, payment.GetProperty("orderId").ValueKind);

        var records = await WebhookTestClient.GetReconciliationAsync(client, owner, paymentId);
        var record = Assert.Single(records);
        Assert.Equal("Unmatched", record.GetProperty("outcome").GetString());
        Assert.Equal("None", record.GetProperty("strategy").GetString());

        var orders = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/orders?pageSize=25",
            owner));
        orders.EnsureSuccessStatusCode();
        var ordersBody = await orders.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, ordersBody.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Payment_before_order_can_be_reconciled_later()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whpreorder"));
        var orderNumber = $"LATE-{Guid.NewGuid().ToString("N")[..8]}";
        var eventId = WebhookTestClient.NewEventId();
        var providerPaymentId = WebhookTestClient.NewProviderPaymentId();

        var ack = await SendAsync(
            client,
            owner,
            eventId,
            providerPaymentId,
            100m,
            reference: orderNumber);
        Assert.Equal(HttpStatusCode.Accepted, ack.StatusCode);

        var webhook = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed");
        var paymentId = webhook.GetProperty("paymentId").GetGuid();

        var beforeOrder = await WebhookTestClient.GetPaymentAsync(client, owner, paymentId);
        Assert.Equal("Unmatched", beforeOrder.GetProperty("status").GetString());

        var orderId = await client.CreateOrderAsync(owner, 100m, orderNumber);

        var reconcile = await client.SendAsync(client.Authorized(
            HttpMethod.Post,
            $"/api/payments/{paymentId}/reconcile",
            owner));
        Assert.Equal(HttpStatusCode.Created, reconcile.StatusCode);
        var reconciled = await reconcile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Matched", reconciled.GetProperty("outcome").GetString());

        var payment = await WebhookTestClient.GetPaymentAsync(client, owner, paymentId);
        Assert.Equal("Matched", payment.GetProperty("status").GetString());
        Assert.Equal(orderId, payment.GetProperty("orderId").GetGuid());

        var order = await WebhookTestClient.GetOrderAsync(client, owner, orderId);
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        Assert.Equal(100m, order.GetProperty("paidAmount").GetDecimal());

        var records = await WebhookTestClient.GetReconciliationAsync(client, owner, paymentId);
        Assert.Contains(records, record => record.GetProperty("outcome").GetString() == "Unmatched");
        Assert.Contains(records, record => record.GetProperty("outcome").GetString() == "Matched");
    }

    [Fact]
    public async Task Failed_payment_event_records_failed_payment_only()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whfailed"));
        var orderId = await client.CreateOrderAsync(owner, 100m);
        var eventId = WebhookTestClient.NewEventId();
        var providerPaymentId = WebhookTestClient.NewProviderPaymentId();
        var payload = WebhookTestClient.PaymentPayload(
            eventId,
            providerPaymentId,
            100m,
            orderId: orderId,
            type: "payment.failed",
            failureReason: "Card declined");

        var ack = await WebhookTestClient.SendSignedAsync(client, owner.TenantSlug, payload);
        Assert.Equal(HttpStatusCode.Accepted, ack.StatusCode);

        var webhook = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed");
        var paymentId = webhook.GetProperty("paymentId").GetGuid();

        var payment = await WebhookTestClient.GetPaymentAsync(client, owner, paymentId);
        Assert.Equal("Failed", payment.GetProperty("status").GetString());
        Assert.Equal("Card declined", payment.GetProperty("failureReason").GetString());

        var order = await WebhookTestClient.GetOrderAsync(client, owner, orderId);
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.Equal(0m, order.GetProperty("paidAmount").GetDecimal());

        var records = await WebhookTestClient.GetReconciliationAsync(client, owner, paymentId);
        Assert.Empty(records);
    }

    [Fact]
    public async Task Multiple_payment_attempts_are_retained()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whattempts"));
        var orderId = await client.CreateOrderAsync(owner, 100m);

        var firstProviderId = WebhookTestClient.NewProviderPaymentId();
        var firstEventId = WebhookTestClient.NewEventId();
        var firstAck = await SendAsync(
            client,
            owner,
            firstEventId,
            firstProviderId,
            90m,
            orderId: orderId);
        Assert.Equal(HttpStatusCode.Accepted, firstAck.StatusCode);

        var firstEvent = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            firstEventId,
            item => item.GetProperty("status").GetString() == "Processed");
        var firstPaymentId = firstEvent.GetProperty("paymentId").GetGuid();

        var afterFirst = await WebhookTestClient.GetOrderAsync(client, owner, orderId);
        Assert.Equal("Pending", afterFirst.GetProperty("status").GetString());
        Assert.Equal(0m, afterFirst.GetProperty("paidAmount").GetDecimal());

        var secondProviderId = WebhookTestClient.NewProviderPaymentId();
        var secondEventId = WebhookTestClient.NewEventId();
        var secondAck = await SendAsync(
            client,
            owner,
            secondEventId,
            secondProviderId,
            100m,
            orderId: orderId);
        Assert.Equal(HttpStatusCode.Accepted, secondAck.StatusCode);

        var secondEvent = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            secondEventId,
            item => item.GetProperty("status").GetString() == "Processed");
        var secondPaymentId = secondEvent.GetProperty("paymentId").GetGuid();

        var firstPayment = await WebhookTestClient.GetPaymentAsync(client, owner, firstPaymentId);
        var secondPayment = await WebhookTestClient.GetPaymentAsync(client, owner, secondPaymentId);
        Assert.Equal("Unmatched", firstPayment.GetProperty("status").GetString());
        Assert.Equal("Matched", secondPayment.GetProperty("status").GetString());

        var firstSearch = await WebhookTestClient.GetPaymentsAsync(client, owner, search: firstProviderId);
        var secondSearch = await WebhookTestClient.GetPaymentsAsync(client, owner, search: secondProviderId);
        Assert.Equal(1, firstSearch.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, secondSearch.GetProperty("totalCount").GetInt32());

        var order = await WebhookTestClient.GetOrderAsync(client, owner, orderId);
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        Assert.Equal(100m, order.GetProperty("paidAmount").GetDecimal());

        var firstRecords = await WebhookTestClient.GetReconciliationAsync(client, owner, firstPaymentId);
        var secondRecords = await WebhookTestClient.GetReconciliationAsync(client, owner, secondPaymentId);
        Assert.Equal("AmountMismatch", Assert.Single(firstRecords).GetProperty("outcome").GetString());
        Assert.Equal("Matched", Assert.Single(secondRecords).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Invalid_webhook_payload_is_stored_as_failed()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whinvalid"));
        var eventId = WebhookTestClient.NewEventId();
        var payload = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "payment.succeeded",
            data = new { }
        });

        var ack = await WebhookTestClient.SendSignedAsync(client, owner.TenantSlug, payload);
        Assert.Equal(HttpStatusCode.Accepted, ack.StatusCode);

        var webhook = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Failed");
        Assert.False(string.IsNullOrWhiteSpace(webhook.GetProperty("failureReason").GetString()));

        var payments = await WebhookTestClient.GetPaymentsAsync(client, owner);
        Assert.Equal(0, payments.GetProperty("totalCount").GetInt32());

        var reconciliation = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            "/api/reconciliation?pageSize=10",
            owner));
        reconciliation.EnsureSuccessStatusCode();
        var reconciliationBody = await reconciliation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, reconciliationBody.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Invalid_signature_is_rejected_without_storing_event()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whsignature"));
        var eventId = WebhookTestClient.NewEventId();
        var payload = WebhookTestClient.PaymentPayload(
            eventId,
            WebhookTestClient.NewProviderPaymentId(),
            100m,
            reference: $"SIG-{Guid.NewGuid():N}");

        var response = await WebhookTestClient.SendSignedAsync(
            client,
            owner.TenantSlug,
            payload,
            signature: "sha256=deadbeef");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var events = await client.SendAsync(client.Authorized(
            HttpMethod.Get,
            $"/api/webhooks?search={Uri.EscapeDataString(eventId)}&pageSize=10",
            owner));
        events.EnsureSuccessStatusCode();
        var eventsBody = await events.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, eventsBody.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Concurrent_duplicate_webhooks_create_one_payment()
    {
        var client = factory.CreateClient();
        var owner = await client.RegisterTenantAsync(NewSlug("whconcurrent"));
        var orderId = await client.CreateOrderAsync(owner, 100m);
        var eventId = WebhookTestClient.NewEventId();
        var providerPaymentId = WebhookTestClient.NewProviderPaymentId();
        var payload = WebhookTestClient.PaymentPayload(eventId, providerPaymentId, 100m, orderId: orderId);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => WebhookTestClient.SendSignedAsync(client, owner.TenantSlug, payload)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));

        var webhook = await WebhookTestClient.WaitForEventAsync(
            client,
            owner,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed"
                    && item.GetProperty("duplicateCount").GetInt32() >= 1);

        var paymentId = webhook.GetProperty("paymentId").GetGuid();
        var payments = await WebhookTestClient.GetPaymentsAsync(client, owner, orderId: orderId);
        Assert.Equal(1, payments.GetProperty("totalCount").GetInt32());

        var order = await WebhookTestClient.GetOrderAsync(client, owner, orderId);
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        Assert.Equal(100m, order.GetProperty("paidAmount").GetDecimal());

        var records = await WebhookTestClient.GetReconciliationAsync(client, owner, paymentId);
        Assert.Single(records);
    }

    [Fact]
    public async Task Webhook_cannot_update_another_tenants_order()
    {
        var client = factory.CreateClient();
        var first = await client.RegisterTenantAsync(NewSlug("whscopeA"));
        var second = await client.RegisterTenantAsync(NewSlug("whscopeB"));
        var orderId = await client.CreateOrderAsync(first, 100m);
        var eventId = WebhookTestClient.NewEventId();
        var providerPaymentId = WebhookTestClient.NewProviderPaymentId();
        var payload = WebhookTestClient.PaymentPayload(eventId, providerPaymentId, 100m, orderId: orderId);

        var ack = await WebhookTestClient.SendSignedAsync(client, second.TenantSlug, payload);
        Assert.Equal(HttpStatusCode.Accepted, ack.StatusCode);

        var webhook = await WebhookTestClient.WaitForEventAsync(
            client,
            second,
            eventId,
            item => item.GetProperty("status").GetString() == "Processed");
        var paymentId = webhook.GetProperty("paymentId").GetGuid();

        var payment = await WebhookTestClient.GetPaymentAsync(client, second, paymentId);
        Assert.Equal("Unmatched", payment.GetProperty("status").GetString());
        Assert.Equal(orderId, payment.GetProperty("providerOrderId").GetGuid());

        var firstOrder = await WebhookTestClient.GetOrderAsync(client, first, orderId);
        Assert.Equal("Pending", firstOrder.GetProperty("status").GetString());
        Assert.Equal(0m, firstOrder.GetProperty("paidAmount").GetDecimal());

        var firstPayments = await WebhookTestClient.GetPaymentsAsync(client, first);
        Assert.Equal(0, firstPayments.GetProperty("totalCount").GetInt32());

        var records = await WebhookTestClient.GetReconciliationAsync(client, second, paymentId);
        var record = Assert.Single(records);
        Assert.Equal("Unmatched", record.GetProperty("outcome").GetString());
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        TestSession session,
        string eventId,
        string providerPaymentId,
        decimal amount,
        Guid? orderId = null,
        string? reference = null)
    {
        var payload = WebhookTestClient.PaymentPayload(
            eventId,
            providerPaymentId,
            amount,
            orderId: orderId,
            reference: reference);

        return await WebhookTestClient.SendSignedAsync(client, session.TenantSlug, payload);
    }

    private static string NewSlug(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";
}
