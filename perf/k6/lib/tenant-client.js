// Tenant-scoped client. Every request carries the tenant host + the owner's bearer
// token, so the API resolves the tenant from the server, never from the payload.

import http from 'k6/http';
import crypto from 'k6/crypto';
import { check } from 'k6';
import { API_URL, PAGE_SIZE, headersFor, tagsFor, unique } from './fleet.js';

function json(response) {
  try {
    return response.json();
  } catch (_) {
    return null;
  }
}

function expect(response, status, label) {
  return check(response, {
    [`${label}: status ${status}`]: (result) => result.status === status,
  });
}

export function refreshSession(session) {
  const response = http.post(
    `${API_URL}/api/auth/refresh`,
    JSON.stringify({ refreshToken: session.refreshToken }),
    { headers: { Host: session.host, 'content-type': 'application/json' }, tags: tagsFor(session, 'refresh_session') },
  );

  if (!expect(response, 200, 'refresh session')) {
    return false;
  }

  const body = json(response);
  session.accessToken = body.accessToken;
  session.refreshToken = body.refreshToken;
  session.expiresAt = Date.parse(body.accessTokenExpiresAt);
  return true;
}

export function createOrder(session, amount = 100) {
  const payload = {
    customerName: 'Fleet Runner',
    customerReference: unique('REF'),
    description: 'k6 fleet performance order',
    expectedAmount: amount,
    currency: 'USD',
  };

  const response = http.post(`${API_URL}/api/orders`, JSON.stringify(payload), {
    headers: headersFor(session),
    tags: tagsFor(session, 'create_order'),
  });

  return expect(response, 201, 'create order') ? json(response) : null;
}

export function recordPayment(session, order) {
  const payload = {
    provider: 'perf',
    providerPaymentId: unique('perfpay'),
    amount: order.expectedAmount,
    currency: order.currency,
    customerName: 'Fleet Runner',
    customerReference: order.customerReference,
    reference: order.orderNumber,
  };

  const response = http.post(`${API_URL}/api/payments`, JSON.stringify(payload), {
    headers: headersFor(session),
    tags: tagsFor(session, 'record_payment'),
  });

  return expect(response, 201, 'record payment') ? json(response) : null;
}

export function makeWebhook(order, secret) {
  const event = {
    id: unique('evt'),
    type: 'payment.succeeded',
    created: Math.floor(Date.now() / 1000),
    data: {
      paymentId: unique('perfwh'),
      orderId: order.id,
      amount: Math.round(order.expectedAmount * 100),
      currency: order.currency,
      customerName: 'Fleet Runner',
      customerReference: order.customerReference,
      reference: order.orderNumber,
    },
  };

  const rawBody = JSON.stringify(event);
  return { event, rawBody, signature: `sha256=${crypto.hmac('sha256', secret, rawBody, 'hex')}` };
}

export function submitWebhook(session, payload, workflow = 'submit_webhook') {
  const response = http.post(`${API_URL}/api/webhooks/mock`, payload.rawBody, {
    headers: headersFor(session, {
      'X-Provider-Signature': payload.signature,
      'X-Correlation-ID': payload.event.id,
    }),
    tags: tagsFor(session, workflow),
  });

  expect(response, 202, workflow);
  return response;
}

export function reconcileUnmatchedPayment(session) {
  const payload = {
    provider: 'perf',
    providerPaymentId: unique('perfunmatched'),
    amount: 42,
    currency: 'USD',
    customerName: 'Fleet Runner',
    reference: unique('REF-UNKNOWN'),
  };

  const paymentResponse = http.post(`${API_URL}/api/payments`, JSON.stringify(payload), {
    headers: headersFor(session),
    tags: tagsFor(session, 'record_unmatched_payment'),
  });

  if (!expect(paymentResponse, 201, 'record unmatched payment')) {
    return;
  }

  const payment = json(paymentResponse);
  const reconcile = http.post(`${API_URL}/api/payments/${payment.id}/reconcile`, null, {
    headers: headersFor(session),
    tags: tagsFor(session, 'run_reconciliation'),
  });

  expect(reconcile, 201, 'run reconciliation');
}

export function listPayments(session) {
  const response = http.get(`${API_URL}/api/payments?page=1&pageSize=${PAGE_SIZE}`, {
    headers: headersFor(session),
    tags: tagsFor(session, 'list_payments'),
  });

  if (!expect(response, 200, 'list payments')) {
    return;
  }

  const body = json(response);
  check(body, {
    'list payments: has items': (value) => value !== null && Array.isArray(value.items),
    'list payments: has totalCount': (value) => value !== null && typeof value.totalCount === 'number',
  });
}

export function listReconciliation(session) {
  const response = http.get(`${API_URL}/api/reconciliation?page=1&pageSize=${PAGE_SIZE}`, {
    headers: headersFor(session),
    tags: tagsFor(session, 'list_reconciliation'),
  });

  if (!expect(response, 200, 'list reconciliation')) {
    return;
  }

  const body = json(response);
  check(body, {
    'list reconciliation: has items': (value) => value !== null && Array.isArray(value.items),
  });
}

export function readWebhookSecret(session) {
  const response = http.get(`${API_URL}/api/tenants/current/webhook-secrets/mock`, {
    headers: headersFor(session),
    tags: tagsFor(session, 'read_webhook_secret'),
  });

  if (!expect(response, 200, 'read webhook secret')) {
    return null;
  }

  return json(response)?.secret ?? null;
}

// Idempotency: replay one event id many times, expecting a 202 for every delivery.
export function replayWebhook(session, payload, count) {
  for (let index = 0; index < count; index += 1) {
    submitWebhook(session, payload, 'duplicate_webhook');
  }
}

// Idempotency: the same provider payment id arrives concurrently for one order.
// The API must record exactly one payment and settle the order exactly once.
export function duplicatePaymentBurst(session, count, batch) {
  const order = createOrder(session, 55);
  if (!order) {
    return null;
  }

  const providerPaymentId = unique('perfdup');
  const payload = JSON.stringify({
    provider: 'perf',
    providerPaymentId,
    amount: order.expectedAmount,
    currency: order.currency,
    customerName: 'Fleet Runner',
    customerReference: order.customerReference,
    reference: order.orderNumber,
  });

  const batchSize = Math.max(1, batch);

  for (let index = 0; index < count; index += batchSize) {
    const requests = [];
    for (let inner = 0; inner < Math.min(batchSize, count - index); inner += 1) {
      requests.push({
        method: 'POST',
        url: `${API_URL}/api/payments`,
        body: payload,
        params: {
          headers: headersFor(session),
          tags: tagsFor(session, 'duplicate_payment'),
          responseCallback: http.expectedStatuses(201, 409),
        },
      });
    }

    const responses = http.batch(requests);
    check(responses, {
      'duplicate payment: no delivery errored': (results) =>
        results.every((result) => result.status === 201 || result.status === 409),
    });
    check(responses, {
      'duplicate payment: every accepted delivery returns one payment id': (results) => {
        const ids = new Set(
          results.filter((result) => result.status === 201).map((result) => json(result)?.id).filter(Boolean),
        );
        return ids.size === 1;
      },
    });
  }

  const detail = http.get(`${API_URL}/api/orders/${order.id}`, {
    headers: headersFor(session),
    tags: tagsFor(session, 'check_duplicate_settlement'),
  });

  if (expect(detail, 200, 'check duplicate settlement')) {
    const body = json(detail);
    check(body, {
      'duplicate payment: order settled exactly once': (value) =>
        value !== null && value.status === 'Paid' && value.paidAmount === value.expectedAmount,
    });
  }

  return providerPaymentId;
}

// Concurrency: two payments race for one order. Exactly one may settle it, and the
// order must never report paidAmount above expectedAmount.
export function concurrentPaymentsForOrder(session) {
  const order = createOrder(session, 30);
  if (!order) {
    return;
  }

  const requests = [0, 1].map(() => ({
    method: 'POST',
    url: `${API_URL}/api/payments`,
    body: JSON.stringify({
      provider: 'perf',
      providerPaymentId: unique('perfrace'),
      amount: order.expectedAmount,
      currency: order.currency,
      customerName: 'Fleet Runner',
      customerReference: order.customerReference,
      reference: order.orderNumber,
    }),
    params: { headers: headersFor(session), tags: tagsFor(session, 'concurrent_payment') },
  }));

  const responses = http.batch(requests);
  check(responses, {
    'concurrent payment: both deliveries answered 201': (results) =>
      results.every((result) => result.status === 201),
  });

  // Let reconciliation handlers settle, then assert the order is never overpaid.
  const detail = http.get(`${API_URL}/api/orders/${order.id}`, {
    headers: headersFor(session),
    tags: tagsFor(session, 'check_order_settlement'),
  });

  if (!expect(detail, 200, 'check order settlement')) {
    return;
  }

  const body = json(detail);
  check(body, {
    'concurrent payment: order not overpaid': (value) =>
      value !== null && value.paidAmount <= value.expectedAmount,
  });
}
