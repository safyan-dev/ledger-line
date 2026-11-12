// http layer to make requests to the API and check responses. 
import http from 'k6/http';
import crypto from 'k6/crypto';
import { check } from 'k6';
import { API_URL, PAGE_SIZE, WEBHOOK_SECRET, requestTags, unique } from './env.js';

const JSON_HEADERS = { 'content-type': 'application/json' };

function safeJson(response) {
  try {
    return response.json();
  } catch (_) {
    return null;
  }
}

function checkResponse(response, expected, label) {
  return check(response, {
    [`${label}: status ${expected}`]: (result) => result.status === expected,
  });
}

export function createOrder(amount = 100) {
  const payload = {
    customerName: 'Perf Runner',
    customerReference: unique('PERF'),
    description: 'k6 performance test order',
    expectedAmount: amount,
    currency: 'USD',
  };

  const response = http.post(`${API_URL}/api/orders`, JSON.stringify(payload), {
    headers: JSON_HEADERS,
    tags: requestTags('create_order'),
  });

  if (!checkResponse(response, 201, 'create order')) {
    return null;
  }

  return safeJson(response);
}

export function recordPayment(order) {
  const payload = {
    provider: 'perf',
    providerPaymentId: unique('perfpay'),
    amount: order.expectedAmount,
    currency: order.currency,
    customerName: 'Perf Runner',
    customerReference: order.customerReference,
    reference: order.orderNumber,
  };

  const response = http.post(`${API_URL}/api/payments`, JSON.stringify(payload), {
    headers: JSON_HEADERS,
    tags: requestTags('record_payment'),
  });

  if (!checkResponse(response, 201, 'record payment')) {
    return null;
  }

  return safeJson(response);
}

export function makeWebhook(order) {
  const event = {
    id: unique('evt-perf'),
    type: 'payment.succeeded',
    created: Math.floor(Date.now() / 1000),
    data: {
      paymentId: unique('perfwh'),
      orderId: order.id,
      amount: Math.round(order.expectedAmount * 100),
      currency: order.currency,
      customerName: 'Perf Runner',
      customerReference: order.customerReference,
      reference: order.orderNumber,
    },
  };

  const rawBody = JSON.stringify(event);
  const signature = `sha256=${crypto.hmac('sha256', WEBHOOK_SECRET, rawBody, 'hex')}`;

  return { event, rawBody, signature };
}

export function submitWebhook(payload, workflow = 'submit_webhook') {
  const response = http.post(`${API_URL}/api/webhooks/mock`, payload.rawBody, {
    headers: {
      'content-type': 'application/json',
      'X-Provider-Signature': payload.signature,
      'X-Correlation-ID': payload.event.id,
    },
    tags: requestTags(workflow),
  });

  checkResponse(response, 202, workflow);
  return response;
}

export function duplicateWebhook(payload, count = 3) {
  for (let index = 0; index < count; index += 1) {
    submitWebhook(payload, 'duplicate_webhook');
  }
}

export function reconcileUnmatchedPayment() {
  const providerPaymentId = unique('perfunmatched');
  const payload = {
    provider: 'perf',
    providerPaymentId,
    amount: 42,
    currency: 'USD',
    customerName: 'Perf Runner',
    reference: unique('PERF-UNKNOWN'),
  };

  const paymentResponse = http.post(`${API_URL}/api/payments`, JSON.stringify(payload), {
    headers: JSON_HEADERS,
    tags: requestTags('record_unmatched_payment'),
  });

  if (!checkResponse(paymentResponse, 201, 'record unmatched payment')) {
    return;
  }

  const payment = safeJson(paymentResponse);
  if (!payment) {
    return;
  }

  const reconcileResponse = http.post(`${API_URL}/api/payments/${payment.id}/reconcile`, null, {
    tags: requestTags('run_reconciliation'),
  });

  checkResponse(reconcileResponse, 201, 'run reconciliation');
}

export function listPayments() {
  const response = http.get(`${API_URL}/api/payments?page=1&pageSize=${PAGE_SIZE}`, {
    tags: requestTags('list_payments'),
  });

  if (!checkResponse(response, 200, 'list payments')) {
    return;
  }

  const body = safeJson(response);
  check(body, {
    'list payments: has items': (value) => value !== null && Array.isArray(value.items),
    'list payments: has totalCount': (value) => value !== null && typeof value.totalCount === 'number',
  });
}

export function listReconciliation() {
  const response = http.get(`${API_URL}/api/reconciliation?page=1&pageSize=${PAGE_SIZE}`, {
    tags: requestTags('list_reconciliation'),
  });

  if (!checkResponse(response, 200, 'list reconciliation')) {
    return;
  }

  const body = safeJson(response);
  check(body, {
    'list reconciliation: has items': (value) => value !== null && Array.isArray(value.items),
    'list reconciliation: has totalCount': (value) => value !== null && typeof value.totalCount === 'number',
  });
}
