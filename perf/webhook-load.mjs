#!/usr/bin/env node
/**
 * Continuous signed-webhook load generator for the perf fleet.
 *
 * Runs alongside perf/run-fleet.sh and keeps sending real provider traffic:
 * it creates an order, then delivers a signed payment.succeeded webhook for it,
 * optionally replaying the same event id to exercise duplicate handling.
 *
 * Usage:
 *   node perf/webhook-load.mjs \
 *     --api "http://<slug>.localhost:5082" \
 *     --tenant <slug> --email <owner email> --password <owner password> \
 *     --secret <tenant mock webhook secret> \
 *     --rate 10 --duration 300 --duplicates 2
 *
 * Options:
 *   --api           tenant base URL, e.g. http://acme.localhost:5082   (required)
 *   --tenant        tenant slug                                        (required)
 *   --email         owner email                                        (required)
 *   --password      owner password                                     (required)
 *   --secret        tenant mock webhook signing secret                 (required)
 *   --rate          target webhook deliveries per second (default 5)
 *   --duration      run seconds (default 120)
 *   --concurrency   in-flight workers (default 4)
 *   --duplicates    extra replays of the same event id (default 1)
 *   --provider      provider path segment (default mock)
 */

import { createHmac } from 'node:crypto'

const args = process.argv.slice(2)

function option(name, fallback) {
  const index = args.indexOf(`--${name}`)
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback
}

const API = option('api', '').replace(/\/$/, '')
const TENANT = option('tenant', '')
const EMAIL = option('email', '')
const PASSWORD = option('password', '')
const SECRET = option('secret', '')
const RATE = Math.max(0.1, Number(option('rate', '5')))
const DURATION = Math.max(1, Number(option('duration', '120')))
const CONCURRENCY = Math.max(1, Number(option('concurrency', '4')))
const DUPLICATES = Math.max(0, Number(option('duplicates', '1')))
const PROVIDER = option('provider', 'mock')

for (const [name, value] of [
  ['--api', API],
  ['--tenant', TENANT],
  ['--email', EMAIL],
  ['--password', PASSWORD],
  ['--secret', SECRET],
]) {
  if (!value) {
    console.error(`Missing ${name}. See the header of perf/webhook-load.mjs.`)
    process.exit(2)
  }
}

let accessToken = null
let counter = 0
const ackTimes = []
const stats = {
  orders: 0,
  created: 0,
  deliveries: 0,
  accepted: 0,
  rejected: 0,
  retried: 0,
  errors: 0,
}

function uniqueId(prefix) {
  counter += 1
  return `${prefix}-${Date.now().toString(36)}-${counter}-${Math.random().toString(36).slice(2, 6)}`
}

function sign(rawBody) {
  return `sha256=${createHmac('sha256', SECRET).update(rawBody).digest('hex')}`
}

async function authenticate() {
  const response = await fetch(`${API}/api/auth/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ email: EMAIL, password: PASSWORD }),
  })

  if (!response.ok) {
    throw new Error(`login failed (${response.status})`)
  }

  accessToken = (await response.json()).accessToken
}

async function api(path, init = {}) {
  if (!accessToken) {
    await authenticate()
  }

  const response = await fetch(`${API}${path}`, {
    ...init,
    headers: {
      'X-Tenant-Slug': TENANT,
      Authorization: `Bearer ${accessToken}`,
      ...(init.headers ?? {}),
    },
  })

  if (response.status === 401) {
    accessToken = null
    await authenticate()
    return api(path, init)
  }

  const text = await response.text()
  if (!response.ok) {
    throw new Error(`${init.method ?? 'GET'} ${path} -> ${response.status}: ${text.slice(0, 200)}`)
  }

  return text ? JSON.parse(text) : null
}

async function createOrder() {
  const order = await api('/api/orders', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      customerName: 'Webhook Load',
      customerReference: uniqueId('WHREF'),
      description: 'webhook-load.mjs',
      expectedAmount: 100,
      currency: 'USD',
    }),
  })

  stats.orders += 1
  return order
}

async function deliver(rawBody, signature) {
  const startedAt = performance.now()

  try {
    const response = await fetch(`${API}/api/webhooks/${PROVIDER}`, {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-Provider-Signature': signature,
        'X-Tenant-Slug': TENANT,
      },
      body: rawBody,
    })

    await response.text()
    const elapsed = performance.now() - startedAt
    ackTimes.push(elapsed)
    stats.deliveries += 1

    if (response.status === 202) {
      stats.accepted += 1
    } else if (response.status === 401 || response.status === 400) {
      stats.rejected += 1
    } else {
      stats.retried += 1
    }
  } catch (error) {
    stats.errors += 1
  }
}

async function oneEvent() {
  const order = await createOrder()
  const event = {
    id: uniqueId('evt-whload'),
    type: 'payment.succeeded',
    created: Math.floor(Date.now() / 1000),
    data: {
      paymentId: uniqueId('pay-whload'),
      orderId: order.id,
      amount: Math.round(order.expectedAmount * 100),
      currency: order.currency,
      customerName: 'Webhook Load',
      customerReference: order.customerReference,
      reference: order.orderNumber,
    },
  }

  const rawBody = JSON.stringify(event)
  const signature = sign(rawBody)

  await deliver(rawBody, signature)
  for (let index = 0; index < DUPLICATES; index += 1) {
    await deliver(rawBody, signature)
  }
}

function percentile(values, fraction) {
  if (values.length === 0) return 0
  const sorted = [...values].sort((left, right) => left - right)
  const index = Math.min(sorted.length - 1, Math.floor(sorted.length * fraction))
  return sorted[index]
}

function report() {
  const p = (fraction) => percentile(ackTimes, fraction).toFixed(1)
  console.log(
    `  deliveries=${stats.deliveries} accepted=${stats.accepted} rejected=${stats.rejected} ` +
      `retried=${stats.retried} errors=${stats.errors} orders=${stats.orders} ` +
      `ack p50=${p(0.5)}ms p95=${p(0.95)}ms p99=${p(0.99)}ms`,
  )
}

async function worker(deadline, intervalMs) {
  while (Date.now() < deadline) {
    const startedAt = Date.now()
    try {
      await oneEvent()
    } catch (error) {
      stats.errors += 1
    }

    const remaining = intervalMs - (Date.now() - startedAt)
    if (remaining > 0) {
      await new Promise((resolve) => setTimeout(resolve, remaining))
    }
  }
}

async function main() {
  await authenticate()
  console.log(`Webhook load → ${API} (tenant ${TENANT}, ${RATE}/s for ${DURATION}s, ${CONCURRENCY} workers)`)

  const deadline = Date.now() + DURATION * 1000
  const intervalMs = (CONCURRENCY * 1000) / RATE
  const ticker = setInterval(report, 2000)

  await Promise.all(Array.from({ length: CONCURRENCY }, () => worker(deadline, intervalMs)))

  clearInterval(ticker)
  console.log('')
  report()
  console.log(`done in ${DURATION}s`)
}

await main()
