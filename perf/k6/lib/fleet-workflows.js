// One iteration of a tenant owner's workload: create orders, record payments,
// receive signed provider webhooks (including replays), reconcile, and browse.

import {
  concurrentPaymentsForOrder,
  createOrder,
  duplicatePaymentBurst,
  listPayments,
  listReconciliation,
  makeWebhook,
  reconcileUnmatchedPayment,
  recordPayment,
  replayWebhook,
  submitWebhook,
} from './tenant-client.js';

export function runWorkflow(session) {
  const order = createOrder(session, 100);
  if (order) {
    recordPayment(session, order);
  }

  const webhookOrder = createOrder(session, 75);
  if (webhookOrder) {
    const webhook = makeWebhook(webhookOrder, session.tenant.webhookSecret);
    submitWebhook(session, webhook);

    // Idempotency: the provider retries the same event id; every delivery must ack.
    replayWebhook(session, webhook, 3);
  }

  // Idempotency: the same provider payment id arrives repeatedly from one tenant.
  duplicatePaymentBurst(session, 4, 4);

  // Concurrency: two settlements race for the same order.
  concurrentPaymentsForOrder(session);

  reconcileUnmatchedPayment(session);
  listPayments(session);
  listReconciliation(session);
}
