// Runs one complete iteration:
// create order, 
// record payment, 
// make webhook, 
// submit webhook, 
// duplicate webhook, 
// reconcile unmatched payment, 
// list payments and list reconciliation.
import {
  createOrder,
  duplicateWebhook,
  listPayments,
  listReconciliation,
  makeWebhook,
  reconcileUnmatchedPayment,
  recordPayment,
  submitWebhook,
} from './client.js';

export function runWorkflow() {
  const order = createOrder(100);
  if (order) {
    recordPayment(order);
  }

  const webhookOrder = createOrder(75);
  if (webhookOrder) {
    const webhook = makeWebhook(webhookOrder);
    submitWebhook(webhook);
    duplicateWebhook(webhook, 3);
  }

  reconcileUnmatchedPayment();
  listPayments();
  listReconciliation();
}
