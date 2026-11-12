#!/usr/bin/env node
/**
 * Ledgerline webhook provider simulator.
 *
 * Sends signed provider events to POST /api/webhooks/{provider}, retries when the
 * acknowledgement is missing, and verifies the asynchronous outcome through the API.
 *
 * Usage:
 *   node scripts/simulate-webhooks.mjs --scenario all
 *   node scripts/simulate-webhooks.mjs --scenario success
 *   node scripts/simulate-webhooks.mjs --scenario concurrent --iterations 20
 *
 * Options:
 *   --api           API base URL           (default http://localhost:5080)
 *   --secret        webhook signing secret (default whsec_dev_ledgerline)
 *   --scenario      all|e2e|E2E-01..E2E-06|concurrent|conflict|unknown|missing-order|invalid|signature|failed-payment
 *   --iterations    concurrent delivery count (default 10)
 *   --timeout-ms    client ack timeout before retry (default 1000 ms)
 *   --ack-budget-ms server-side ack budget asserted from X-Webhook-Ack-Ms (default 5 ms)
 *   --retries       retry attempts for a missing/slow/failed ack (default 3)
 *   --order-number  late-arriving order number for E2E-06 (default ORD-2001)
 */

import { createHmac, randomUUID } from 'node:crypto'

const args = process.argv.slice(2)

function option(name, fallback) {
  const index = args.indexOf(`--${name}`)
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback
}

const API = option('api', process.env.API_URL ?? 'http://localhost:5080').replace(/\/$/, '')
const SECRET = option('secret', process.env.WEBHOOK_SECRET ?? 'whsec_dev_ledgerline')
const SCENARIO = option('scenario', 'all')
const ITERATIONS = Number(option('iterations', '10'))
const ACK_TIMEOUT_MS = Number(option('timeout-ms', '1000'))
const ACK_BUDGET_MS = Number(option('ack-budget-ms', '5'))
const RETRIES = Number(option('retries', '3'))
const PROVIDER = option('provider', 'mock')
const LATE_ORDER_NUMBER = option('order-number', 'ORD-2001')

const results = []
let requestCounter = 0

function uniqueId(prefix) {
  requestCounter += 1
  return `${prefix}_${Date.now().toString(36)}_${requestCounter}_${Math.random().toString(36).slice(2, 7)}`
}

function sign(rawBody) {
  return `sha256=${createHmac('sha256', SECRET).update(rawBody).digest('hex')}`
}

async function api(path, init) {
  const response = await fetch(`${API}${path}`, init)
  const text = await response.text()

  if (!response.ok) {
    throw new Error(`${init?.method ?? 'GET'} ${path} -> ${response.status}: ${text.slice(0, 300)}`)
  }

  return text ? JSON.parse(text) : null
}

async function createOrder(expectedAmount, label = 'SIM', options = {}) {
  const { customerReference, orderNumber } = options
  const payload = {
    customerName: 'Simulator Customer',
    customerReference: customerReference ?? uniqueId(label),
    description: 'Created by simulate-webhooks.mjs',
    expectedAmount,
    currency: 'USD',
  }

  if (orderNumber) payload.orderNumber = orderNumber

  return api('/api/orders', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(payload),
  })
}

async function getOrder(orderId) {
  return api(`/api/orders/${orderId}`)
}

async function getOrderOrNull(orderId) {
  const response = await fetch(`${API}/api/orders/${orderId}`)

  if (response.status === 404) return null
  if (!response.ok) throw new Error(`GET /api/orders/${orderId} -> ${response.status}`)

  return response.json()
}

async function getDashboard() {
  return api('/api/dashboard/summary')
}

async function getReconciliation(paymentId) {
  return api(`/api/reconciliation/payment/${paymentId}`)
}

async function getAuditLogs(entityId) {
  return api(`/api/audit-logs?entityId=${encodeURIComponent(entityId)}&pageSize=100`)
}

async function getPayments(providerPaymentId) {
  return api(`/api/payments?search=${encodeURIComponent(providerPaymentId)}&pageSize=10`)
}

async function findPayment(providerPaymentId) {
  const page = await api(`/api/payments?search=${encodeURIComponent(providerPaymentId)}&pageSize=10`)
  return page.items.find((payment) => payment.providerPaymentId === providerPaymentId)
}

async function findWebhook(providerEventId) {
  const page = await api(`/api/webhooks?search=${encodeURIComponent(providerEventId)}&pageSize=10`)
  return page.items.find((event) => event.providerEventId === providerEventId)
}

async function waitFor(description, probe, timeoutMs = 4000, intervalMs = 100) {
  const deadline = Date.now() + timeoutMs

  while (Date.now() < deadline) {
    const value = await probe()
    if (value) return value
    await new Promise((resolve) => setTimeout(resolve, intervalMs))
  }

  throw new Error(`Timed out waiting for ${description}`)
}

function makePaymentEvent({
  eventId,
  type = 'payment.succeeded',
  paymentId,
  orderId,
  amount,
  currency = 'USD',
  includeOrderId = true,
  includePaymentId = true,
  includeAmount = true,
  failureReason,
  customerReference,
  reference,
}) {
  const data = {
    currency,
    customerName: 'Simulator Customer',
  }

  if (includePaymentId) data.paymentId = paymentId
  if (includeOrderId) data.orderId = orderId
  if (includeAmount) data.amount = Math.round(amount * 100)
  if (failureReason) data.failureReason = failureReason
  if (customerReference) data.customerReference = customerReference
  if (reference) data.reference = reference

  return {
    id: eventId,
    type,
    created: Math.floor(Date.now() / 1000),
    data,
  }
}

async function postSigned(rawBody, signature = sign(rawBody), timeoutMs = ACK_TIMEOUT_MS, extraHeaders = {}) {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  const startedAt = performance.now()

  try {
    const response = await fetch(`${API}/api/webhooks/${PROVIDER}`, {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-Provider-Signature': signature,
        ...extraHeaders,
      },
      body: rawBody,
      signal: controller.signal,
    })

    const body = await response.text()
    const ackHeader = response.headers.get('x-webhook-ack-ms')

    return {
      status: response.status,
      body,
      serverAckMs: ackHeader === null ? null : Number(ackHeader),
      roundTripMs: Number((performance.now() - startedAt).toFixed(3)),
    }
  } finally {
    clearTimeout(timer)
  }
}

async function sendWithRetry(event, { timeoutMs = ACK_TIMEOUT_MS, headers = {} } = {}) {
  const rawBody = JSON.stringify(event)
  let lastError
  let retries = 0

  for (let attempt = 0; attempt <= RETRIES; attempt += 1) {
    try {
      const response = await postSigned(rawBody, sign(rawBody), timeoutMs, headers)

      if (response.status === 401) return { ...response, retries }

      if (response.status === 202) {
        const slowAck =
          response.serverAckMs !== null && response.serverAckMs > ACK_BUDGET_MS

        if (slowAck && attempt < RETRIES) {
          lastError = new Error(
            `server ack ${response.serverAckMs} ms exceeded the ${ACK_BUDGET_MS} ms budget`,
          )
          retries += 1
          continue
        }

        return { ...response, retries }
      }

      if (response.status === 503 || response.status >= 500) {
        lastError = new Error(`ack returned ${response.status}`)
        retries += 1
        continue
      }

      return { ...response, retries }
    } catch (error) {
      lastError = error
      retries += 1
    }
  }

  throw new Error(`No acknowledgement after ${RETRIES} retries: ${lastError?.message ?? 'unknown error'}`)
}

function assert(condition, message) {
  if (!condition) throw new Error(message)
}

function assertAck(response, label) {
  if (response.serverAckMs === null) return
  assert(
    response.serverAckMs <= ACK_BUDGET_MS,
    `${label}: server ack took ${response.serverAckMs} ms (budget ${ACK_BUDGET_MS} ms)`,
  )
}

async function warmup() {
  // Warm the HMAC/JIT path with an invalid signature; nothing is stored.
  try {
    await postSigned('{"id":"warmup","type":"payment.succeeded","data":{}}', 'sha256=deadbeef')
  } catch {
    // ignored
  }
}

function orderPaymentEvent(order, amount, paymentId = `pay_${order.id}`) {
  return makePaymentEvent({
    eventId: order.id,
    paymentId,
    orderId: order.id,
    amount,
  })
}

const scenarios = {
  'E2E-01': async () => {
    const before = await getDashboard()
    const order = await createOrder(100, 'E2E-01')
    const event = orderPaymentEvent(order, 100)
    const ack = await sendWithRetry(event)
    assert(ack.status === 202, `ack was ${ack.status}`)
    assertAck(ack, 'E2E-01')

    const webhook = await waitFor('webhook to be processed', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Processed' ? value : null
    })

    const payment = await waitFor('payment to be matched', async () => {
      const value = await findPayment(event.data.paymentId)
      return value?.status === 'Matched' ? value : null
    })

    const records = await getReconciliation(payment.id)
    assert(records.length === 1, `expected 1 reconciliation record, got ${records.length}`)
    assert(records[0].outcome === 'Matched', `expected Matched, got ${records[0].outcome}`)

    const paidOrder = await waitFor('order to be Paid', async () => {
      const value = await getOrder(order.id)
      return value.status === 'Paid' ? value : null
    })

    const payments = await getPayments(event.data.paymentId)
    assert(payments.totalCount === 1, `expected 1 payment, got ${payments.totalCount}`)
    assert(webhook.paymentId === payment.id, 'webhook is not linked to the payment')

    const after = await getDashboard()
    assert(after.flow.paymentsRecorded === before.flow.paymentsRecorded + 1, 'dashboard payments count did not increase by 1')
    assert(after.flow.webhooksReceived === before.flow.webhooksReceived + 1, 'dashboard webhook count did not increase by 1')
    assert(after.flow.matched === before.flow.matched + 1, 'dashboard matched count did not increase by 1')
    assert(after.flow.unmatched === before.flow.unmatched, 'successful payment affected the review count')
    assert(after.kpis.pendingReview === before.kpis.pendingReview, 'successful payment entered the review queue')
    assert(paidOrder.paidAmount === 100, `order paid amount is ${paidOrder.paidAmount}`)

    return 'order Paid, payment Matched, webhook Processed; dashboard +1'
  },

  'E2E-02': async () => {
    const order = await createOrder(100, 'E2E-02')
    const event = orderPaymentEvent(order, 100)

    const first = await sendWithRetry(event)
    assert(first.status === 202, `first ack was ${first.status}`)
    assertAck(first, 'E2E-02')
    await waitFor('first event to process', async () => (await findWebhook(event.id))?.status === 'Processed')

    for (let i = 0; i < 3; i += 1) {
      const duplicate = await sendWithRetry(event)
      assert(duplicate.status === 202, `duplicate ack was ${duplicate.status}`)
    }

    const webhook = await waitFor('three duplicate deliveries', async () => {
      const value = await findWebhook(event.id)
      return value && value.duplicateCount >= 3 ? value : null
    })

    const payments = await getPayments(event.data.paymentId)
    assert(payments.totalCount === 1, `expected 1 payment, got ${payments.totalCount}`)
    const payment = payments.items[0]

    const orderAfter = await getOrder(order.id)
    assert(orderAfter.status === 'Paid' && orderAfter.paidAmount === 100, 'order was updated more than once')

    const records = await getReconciliation(payment.id)
    assert(records.filter((record) => record.outcome === 'Matched').length === 1, 'expected exactly one Matched record')

    const audits = await getAuditLogs(payment.id)
    const recorded = audits.items.filter((entry) => entry.action === 'payment.recorded')
    assert(recorded.length === 1, `expected one payment.recorded audit entry, got ${recorded.length}`)

    return `4 deliveries → 1 payment; duplicateCount=${webhook.duplicateCount}`
  },

  'E2E-03': async () => {
    const before = await getDashboard()
    const order = await createOrder(100, 'E2E-03')
    const event = orderPaymentEvent(order, 90)

    const ack = await sendWithRetry(event)
    assert(ack.status === 202, `ack was ${ack.status}`)
    assertAck(ack, 'E2E-03')

    const webhook = await waitFor('webhook to be processed', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Processed' ? value : null
    })
    assert(webhook.paymentId, 'mismatched payment was not recorded')

    const payment = await waitFor('payment to stay unmatched', async () => {
      const value = await findPayment(event.data.paymentId)
      return value?.status === 'Unmatched' ? value : null
    })

    const records = await getReconciliation(payment.id)
    const mismatch = records.find((record) => record.outcome === 'AmountMismatch')
    assert(mismatch, `expected AmountMismatch, got ${records.map((record) => record.outcome).join(', ')}`)
    assert(mismatch.reason?.includes('Amount mismatch'), 'mismatch reason missing')

    const orderAfter = await getOrder(order.id)
    assert(orderAfter.status === 'Pending' && orderAfter.paidAmount === 0, 'mismatched payment updated the order')

    const mismatches = await api(
      `/api/reconciliation?outcome=AmountMismatch&search=${encodeURIComponent(event.data.paymentId)}`,
    )
    assert(mismatches.totalCount >= 1, 'payment is not visible in the mismatch view')

    const after = await getDashboard()
    assert(after.flow.unmatched === before.flow.unmatched + 1, 'dashboard review count did not increase by 1')
    assert(after.kpis.pendingReview === before.kpis.pendingReview + 1, 'pending review did not increase by 1')

    return 'payment retained, AmountMismatch, order untouched, visible in mismatch view'
  },

  'E2E-04': async () => {
    const unknownOrderId = randomUUID()
    const paymentId = `pay_${unknownOrderId}`
    const event = makePaymentEvent({
      eventId: unknownOrderId,
      paymentId,
      orderId: unknownOrderId,
      amount: 50,
    })

    const ack = await sendWithRetry(event)
    assert(ack.status === 202, `ack was ${ack.status}`)
    assertAck(ack, 'E2E-04')

    await waitFor('webhook to be processed', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Processed' ? value : null
    })

    const payment = await waitFor('unknown-order payment to stay unmatched', async () => {
      const value = await findPayment(event.data.paymentId)
      return value?.status === 'Unmatched' ? value : null
    })

    const records = await getReconciliation(payment.id)
    assert(records.some((record) => record.outcome === 'Unmatched'), 'expected an Unmatched reconciliation record')
    assert(await getOrderOrNull(unknownOrderId) === null, 'an order was created for an unknown id')

    const payments = await getPayments(event.data.paymentId)
    assert(payments.totalCount === 1, `expected 1 retained payment, got ${payments.totalCount}`)

    return 'payment retained, Unmatched, no order updated'
  },

  'E2E-05': async () => {
    const order = await createOrder(100, 'E2E-05')
    const event = orderPaymentEvent(order, 100)
    const rawBody = JSON.stringify(event)

    const failedAck = await postSigned(rawBody, sign(rawBody), ACK_TIMEOUT_MS, {
      'X-Simulate-Processing-Failure': '1',
    })
    assert(failedAck.status === 202, `failure ack was ${failedAck.status}`)
    assertAck(failedAck, 'E2E-05')

    const failedWebhook = await waitFor('webhook to fail', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Failed' ? value : null
    })
    assert(failedWebhook.failureReason?.includes('simulated_processing_failure'), `unexpected failure: ${failedWebhook.failureReason}`)

    const beforeRetryPayments = await getPayments(event.data.paymentId)
    assert(beforeRetryPayments.totalCount === 0, 'a failed event created a payment')

    const pendingOrder = await getOrder(order.id)
    assert(pendingOrder.status === 'Pending' && pendingOrder.paidAmount === 0, 'failed event updated the order')

    const retryAck = await sendWithRetry(event)
    assert(retryAck.status === 202, `retry ack was ${retryAck.status}`)
    assertAck(retryAck, 'E2E-05 retry')

    const processed = await waitFor('retried webhook to process', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Processed' ? value : null
    })

    const payment = await waitFor('payment after retry', async () => {
      const value = await findPayment(event.data.paymentId)
      return value?.status === 'Matched' ? value : null
    })

    const paidOrder = await waitFor('order after retry', async () => {
      const value = await getOrder(order.id)
      return value.status === 'Paid' ? value : null
    })

    const payments = await getPayments(event.data.paymentId)
    assert(payments.totalCount === 1, `expected 1 payment after retry, got ${payments.totalCount}`)
    assert(processed.attemptCount >= 2, `expected retry history, attemptCount=${processed.attemptCount}`)

    const audits = await getAuditLogs(failedWebhook.id)
    const actions = audits.items.map((entry) => entry.action)
    assert(actions.includes('webhook.failed'), 'missing webhook.failed audit entry')
    assert(actions.includes('webhook.processed'), 'missing webhook.processed audit entry')
    assert(paidOrder.paidAmount === 100, `order paid amount is ${paidOrder.paidAmount}`)

    return `failed then retried; attemptCount=${processed.attemptCount}; payment matched once`
  },

  'E2E-06': async () => {
    let orderNumber = LATE_ORDER_NUMBER
    const existing = await api(`/api/orders?search=${encodeURIComponent(LATE_ORDER_NUMBER)}&pageSize=50`)
    const orderCollision = existing.items.some((order) => order.orderNumber === LATE_ORDER_NUMBER)
    const webhookCollision = (await findWebhook(LATE_ORDER_NUMBER)) !== undefined

    if (orderCollision || webhookCollision) {
      orderNumber = `${LATE_ORDER_NUMBER}-${Date.now().toString(36).slice(-6)}`
    }

    const paymentId = `pay_late_${Date.now()}`
    const event = makePaymentEvent({
      eventId: orderNumber,
      paymentId,
      amount: 100,
      includeOrderId: false,
      reference: orderNumber,
    })

    const ack = await sendWithRetry(event)
    assert(ack.status === 202, `ack was ${ack.status}`)
    assertAck(ack, 'E2E-06')

    await waitFor('pre-order webhook to process', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Processed' ? value : null
    })

    const payment = await waitFor('payment to wait for its order', async () => {
      const value = await findPayment(paymentId)
      return value?.status === 'Unmatched' ? value : null
    })

    const beforeOrder = await getReconciliation(payment.id)
    assert(beforeOrder.some((record) => record.outcome === 'Unmatched'), 'expected an Unmatched history entry')

    const order = await createOrder(100, 'E2E-06', { orderNumber })
    const reconcile = await api(`/api/payments/${payment.id}/reconcile`, { method: 'POST' })
    assert(reconcile.outcome === 'Matched', `expected Matched, got ${reconcile.outcome}`)

    const paidOrder = await waitFor('late order to become Paid', async () => {
      const value = await getOrder(order.id)
      return value.status === 'Paid' ? value : null
    })

    const afterOrder = await getReconciliation(payment.id)
    assert(afterOrder.length >= 2, `expected reconciliation history, got ${afterOrder.length}`)
    assert(afterOrder.some((record) => record.outcome === 'Unmatched'), 'Unmatched history was lost')
    assert(afterOrder.some((record) => record.outcome === 'Matched'), 'Matched history missing')
    assert(paidOrder.paidAmount === 100, `order paid amount is ${paidOrder.paidAmount}`)

    return `${orderNumber}: Unmatched → Matched; history retained`
  },

  async concurrent() {
    const order = await createOrder(100, 'TECH-CONCURRENT')
    const event = orderPaymentEvent(order, 100)
    const rawBody = JSON.stringify(event)

    const responses = await Promise.all(
      Array.from({ length: ITERATIONS }, () => postSigned(rawBody, sign(rawBody))),
    )

    for (const response of responses) {
      assert(response.status === 202, `concurrent ack was ${response.status}`)
      assertAck(response, 'concurrent')
    }

    const webhook = await waitFor('concurrent duplicate counter', async () => {
      const value = await findWebhook(event.id)
      return value && value.duplicateCount >= ITERATIONS - 1 ? value : null
    }, 6000)

    await waitFor('concurrent payment to match', async () => {
      const value = await findPayment(event.data.paymentId)
      return value?.status === 'Matched' ? value : null
    }, 6000)

    const payments = await getPayments(event.data.paymentId)
    assert(payments.totalCount === 1, `expected 1 payment, got ${payments.totalCount}`)

    const orderAfter = await getOrder(order.id)
    assert(orderAfter.status === 'Paid' && orderAfter.paidAmount === 100, 'order was not updated exactly once')

    return `${ITERATIONS} concurrent posts → 1 payment, paid once; duplicateCount=${webhook.duplicateCount}`
  },

  async conflict() {
    const order = await createOrder(100, 'TECH-CONFLICT')
    const event = orderPaymentEvent(order, 100)

    const first = await sendWithRetry(event)
    assert(first.status === 202, `first ack was ${first.status}`)
    await waitFor('first event to process', async () => (await findWebhook(event.id))?.status === 'Processed')

    const mutated = makePaymentEvent({
      eventId: event.id,
      paymentId: event.data.paymentId,
      orderId: order.id,
      amount: 120,
    })
    const second = await sendWithRetry(mutated)
    assert(second.status === 202, `mutated ack was ${second.status}`)

    const webhook = await waitFor('conflict counter', async () => {
      const value = await findWebhook(event.id)
      return value && value.conflictCount >= 1 ? value : null
    })

    const payment = await findPayment(event.data.paymentId)
    assert(payment?.amount === 100, `payment amount changed to ${payment?.amount}`)
    assert(webhook.lastConflictReason?.includes('different payload'), 'conflict reason missing')

    return `mutated replay rejected; conflictCount=${webhook.conflictCount}`
  },

  async unknown() {
    const event = {
      id: uniqueId('evt_sim_unknown'),
      type: 'customer.created',
      created: Math.floor(Date.now() / 1000),
      data: { customerId: 'cus_sim' },
    }

    const response = await sendWithRetry(event)
    assert(response.status === 202, `ack was ${response.status}`)

    const webhook = await waitFor('ignored event', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Ignored' ? value : null
    })

    assert(webhook.paymentId === null, 'ignored event produced a payment')

    return 'unknown type ignored; no payment'
  },

  async 'missing-order'() {
    const event = makePaymentEvent({
      eventId: uniqueId('evt_sim_missing_order'),
      paymentId: uniqueId('pay_sim_missing_order'),
      orderId: null,
      amount: 10,
      includeOrderId: false,
    })

    const response = await sendWithRetry(event)
    assert(response.status === 202, `ack was ${response.status}`)

    const webhook = await waitFor('failed event', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Failed' ? value : null
    })

    assert(webhook.failureReason?.includes('reference'), `unexpected reason: ${webhook.failureReason}`)

    const payments = await getPayments(event.data.paymentId)
    assert(payments.totalCount === 0, 'missing identifiers produced a payment')

    return 'rejected without identifiers; no payment'
  },

  async invalid() {
    const event = makePaymentEvent({
      eventId: uniqueId('evt_sim_invalid'),
      paymentId: uniqueId('pay_sim_invalid'),
      orderId: randomUUID(),
      amount: 10,
      includeAmount: false,
    })

    const response = await sendWithRetry(event)
    assert(response.status === 202, `ack was ${response.status}`)

    const webhook = await waitFor('failed event', async () => {
      const value = await findWebhook(event.id)
      return value?.status === 'Failed' ? value : null
    })

    assert(webhook.failureReason?.includes('amount'), `unexpected reason: ${webhook.failureReason}`)

    return 'invalid payload rejected; no payment'
  },

  async signature() {
    const event = makePaymentEvent({
      eventId: uniqueId('evt_sim_signature'),
      paymentId: uniqueId('pay_sim_signature'),
      orderId: randomUUID(),
      amount: 10,
    })
    const rawBody = JSON.stringify(event)
    const response = await postSigned(rawBody, 'sha256=deadbeef')

    assert(response.status === 401, `expected 401, got ${response.status}`)

    await new Promise((resolve) => setTimeout(resolve, 400))
    assert(await findWebhook(event.id) === undefined, 'event was stored despite an invalid signature')

    return 'invalid signature rejected; nothing stored'
  },

  async 'failed-payment'() {
    const order = await createOrder(80, 'TECH-FAILED')
    const event = makePaymentEvent({
      eventId: order.id,
      paymentId: `pay_${order.id}`,
      orderId: order.id,
      type: 'payment.failed',
      amount: 80,
      failureReason: 'card_declined',
    })

    const ack = await sendWithRetry(event)
    assert(ack.status === 202, `ack was ${ack.status}`)
    assertAck(ack, 'failed-payment')

    const payment = await waitFor('failed payment', async () => {
      const value = await findPayment(event.data.paymentId)
      return value?.status === 'Failed' ? value : null
    })

    const orderAfter = await getOrder(order.id)
    assert(orderAfter.status === 'Pending' && orderAfter.paidAmount === 0, 'failed payment updated the order')

    return `payment ${payment.providerPaymentId} Failed; order untouched`
  },
}

const e2eScenarios = ['E2E-01', 'E2E-02', 'E2E-03', 'E2E-04', 'E2E-05', 'E2E-06']

const technicalScenarios = [
  'concurrent',
  'conflict',
  'unknown',
  'missing-order',
  'invalid',
  'signature',
  'failed-payment',
]

const allScenarios = [...e2eScenarios, ...technicalScenarios]

async function main() {
  const selected =
    SCENARIO === 'all' ? allScenarios : SCENARIO === 'e2e' ? e2eScenarios : [SCENARIO]
  const unknownScenario = selected.find((name) => !scenarios[name])

  if (unknownScenario) {
    console.error(`Unknown scenario '${unknownScenario}'. Use --scenario ${['all', 'e2e', ...allScenarios].join('|')}`)
    process.exit(2)
  }

  console.log(`Ledgerline webhook simulator → ${API} (provider ${PROVIDER})`)
  await warmup()

  for (const name of selected) {
    try {
      const detail = await scenarios[name]()
      results.push({ scenario: name, status: 'PASS', detail })
      console.log(`PASS  ${name.padEnd(14)} ${detail}`)
    } catch (error) {
      results.push({ scenario: name, status: 'FAIL', detail: error.message })
      console.error(`FAIL  ${name.padEnd(14)} ${error.message}`)
    }
  }

  const failed = results.filter((result) => result.status === 'FAIL')

  console.log('')
  console.log(`${results.length - failed.length}/${results.length} scenarios passed`)

  if (failed.length > 0) {
    process.exit(1)
  }
}

await main()
