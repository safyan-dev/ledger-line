# Performance testing

This directory contains a k6 harness for the dedicated performance database. It does not touch the demo database and does not use real provider credentials.

There are two profiles:

| Profile | Command | What it exercises |
|---|---|---|
| Single-tenant | `perf/run-test.sh` | Orders, payments and reconciliation on one anonymous workspace. |
| Fleet | `perf/run-fleet.sh` | Many real tenants, each with its own owner login, webhook secret and full reconciliation flow. |

Use the fleet profile to watch the system under concurrent multi-tenant load while it registers tenants, signs them in and settles payments.

## Prerequisites

- Docker
- .NET 10 SDK
- Python 3.9+
- k6 (`brew install k6` on macOS, or the [k6 download](https://k6.io/docs/get-started/installation/))

## Start the isolated stack

```bash
# 1. Dedicated Postgres on 5433 and database payment_reconciliation_perf
docker compose -f docker-compose.perf.yml up -d

# 2. Perf API on 5082, OTel service name ledgerline-api-perf
./perf/run-api.sh

# 3. In another terminal: predictable seed data
python3 perf/seed.py --run-id perf-seed-001 --orders 100 --payments 100
```

`run-api.sh` sets `Database__SeedDemoData=false`, so demo rows are not inserted. It uses only the mock provider and the local webhook secret.

## Fleet profile: many tenants, live observation

The fleet profile registers real tenants, confirms their owner email, logs each owner in on their workspace subdomain and runs the full reconciliation workload as that tenant. Every VU owns exactly one tenant, so a tenant is never shared between VUs.

### 1. Start the isolated stack and the observability stack

```bash
docker compose -f docker-compose.perf.yml up -d     # perf Postgres on 5433
docker compose up -d jaeger otel-collector prometheus grafana
./perf/run-api.sh                                  # perf API on 5082, OTel service ledgerline-api-perf
```

### 2. Register and sign in the fleet

```bash
python3 perf/fleet.py --run-id fleet1 --count 50 --plan business
```

This creates `r<run-id><index>` workspaces, reads the confirmation tokens from `backend/src/Api/email-outbox/`, confirms each owner, signs in at `<slug>.localhost:5082`, and stores the access/refresh token plus that tenant's own mock webhook secret in `perf/results/<run-id>-fleet.json`.

Registration is rate limited per IP, so the perf API must be running with `RateLimiting__RegistrationPermitLimit` raised (this is `perf/run-api.sh`). Use the `business` plan: it removes the 500 orders/month ceiling that the default `free` plan enforces.

### 3. Run the load test and watch it live

```bash
PERF_RUN_ID=fleet1 \
PERF_TARGET_VUS=50 \
FLEET_FILE=perf/results/fleet1-fleet.json \
./perf/run-fleet.sh
```

The tenant count must be at least the VU count; the test refuses to start otherwise.

While it runs:

| Tool | URL | Shows |
|---|---|---|
| k6 web dashboard | http://127.0.0.1:5665 | Live RPS, latency percentiles, VUs, checks |
| Grafana | http://localhost:3000 | "Load test" panels plus API, pool and database latency |
| Jaeger | http://localhost:16686 | Per-request traces, including reconciliation and webhook handling |
| Prometheus | http://localhost:9090 | Raw `k6_*`, `http_server_*`, `db_client_*` metrics |
| API log | the `perf/run-api.sh` terminal | Structured JSON request and reconciliation logs |

In Grafana, set the `service` variable to `ledgerline-api-perf` to see the perf API. The dashboard also accepts `ledgerline-api` for the normal stack.

### What each iteration does per tenant

1. Create an order, record a matching payment, let reconciliation settle it.
2. Create a second order and submit a signed `payment.succeeded` webhook, then replay the same event id three times (idempotent duplicate handling).
3. Submit the same provider payment id concurrently four times for one order (idempotency under concurrency).
4. Race two payments against one order (concurrency: exactly one settles, the other is captured as overpaid).
5. Record an unmatched payment and re-run reconciliation.
6. List payments and reconciliation records.

Steps 3 and 4 assert business invariants, not just HTTP status: no duplicate payment is created, and an order is never settled twice. Any violation fails the k6 threshold.

### After the run

`perf/run-fleet.sh` then runs `perf/verify-fleet.py`, which re-reads every tenant as that tenant and fails if any of these is violated:

- duplicate order numbers, customer references or provider payment ids
- an overpaid order, or an order marked Paid with a partial amount
- a matched payment without an order, or an unmatched payment linked to an order
- more than one successful reconciliation record per payment, or a payment with none
- a tenant that recorded no orders or payments at all (catches silent auth failures)

### Reset the fleet

```bash
docker compose -f docker-compose.perf.yml down -v
docker compose -f docker-compose.perf.yml up -d
rm -f perf/results/*-fleet.json
```

The fleet file holds live tokens, so re-seed after a reset. Verification signs each owner in again, so it works even when a long run has rotated the seeded tokens.

### Known behaviour under concurrency

- Sequential duplicate `providerPaymentId` submissions return the original payment (`201` with the same id).
- Concurrent duplicate submissions return `201` for the winner and `409 conflict.duplicate` for the losers. Exactly one payment row is created, and it is the `409`s that appear in the API log as handled `DbUpdateException`s.
- Duplicate webhook event ids are deduplicated by unique index; duplicates increment `DuplicateCount` and are acknowledged with `202`.

### Running the webhook simulator at the same time

`scripts/simulate-webhooks.mjs` is a correctness scenario runner: it signs in, asserts
business outcomes and exits. For sustained provider traffic alongside `run-fleet.sh`, use
`perf/webhook-load.mjs`, which creates orders and delivers signed webhooks continuously
and prints live ack percentiles.

Reserve one tenant that k6 does not drive (seed `VUs + 1` tenants, then run k6 with
`PERF_TARGET_VUS` = tenants - 1):

```bash
T=$(jq -r '.tenants[-1]' perf/results/r2-fleet.json)
S=$(echo "$T" | jq -r .slug)

node perf/webhook-load.mjs \
  --api "http://$S.localhost:5082" \
  --tenant "$S" \
  --email "$(echo "$T" | jq -r .email)" \
  --password "$(echo "$T" | jq -r .password)" \
  --secret "$(echo "$T" | jq -r .webhookSecret)" \
  --rate 10 --duration 300 --duplicates 2
```

Two constraints apply to both tools:

- `--api` must use the tenant subdomain (`http://<slug>.localhost:5082`). Node's fetch strips a manual `Host` header, so a plain `127.0.0.1` URL fails sign-in with 403.
- Use the tenant's own `--secret` from the fleet file. The global `Webhooks:SigningSecret` does not verify for seeded tenants.

`scripts/simulate-webhooks.mjs --scenario e2e` reports `E2E-05` as failed on this build.
Its default 5 ms ack budget is exceeded under any real load, and the underlying
`X-Simulate-Processing-Failure` header is accepted at ingress but never persisted, so the
event processes normally instead of failing. Treat `E2E-05` as expected and use
`--scenario concurrent`, the E2E scenarios you care about, or `perf/webhook-load.mjs`.

## Single-tenant profile

```bash
./perf/run-test.sh baseline perf-baseline-001
./perf/run-test.sh load     perf-load-001
./perf/run-test.sh stress   perf-stress-001
./perf/run-test.sh spike    perf-spike-001
./perf/run-test.sh soak     perf-soak-001
```

Use a **new run id for every run**. IDs appear in order customer references, payment provider ids, webhook event ids and search queries.

| Profile | Shape | Purpose |
|---|---|---|
| baseline | 5 VUs, 1 minute | Establish normal local performance |
| load | 25 VUs, 5 minutes | Expected concurrency |
| stress | ramp to 10 / 25 / 50 / 100 VUs, 2 minutes each | Find degradation |
| spike | 5 → 100 VUs in 10 seconds, hold 1 minute | Sudden burst |
| soak | 20 VUs, 30 minutes | Leaks and long-run stability |

`stress` and `spike` thresholds may fail by design if performance degrades. Do not treat their failure as a product bug until you compare the measured curve and logs.

The runner starts a Prometheus sampler before k6, then runs:
1. k6 workloads.
2. `perf/verify-consistency.py` after the test.

## Workflows exercised per iteration

1. Create an order.
2. Record a matching payment.
3. Create a second order and submit a signed `payment.succeeded` webhook.
4. Replay the same webhook three times with the same event id.
5. Create an unmatched payment and re-run reconciliation.
6. Fetch paginated payments and reconciliation records.

All generated ids include the run id, VU and iteration. The duplicate webhook is intentional; all other business records get unique ids.

## Results

Each run writes to `perf/results/`:

- `*-report.html` — k6 metrics: RPS, average, p50, p95, p99, failures and thresholds.
- `*-summary.json` — raw k6 summary for tooling.
- `*-infra.csv` — CPU, memory, Npgsql pool and database query p95 sampled from Prometheus.
- `*-consistency.json` — order/payment/reconciliation checks.

The baseline measurement is documented in [`RESULTS.md`](./RESULTS.md). No performance improvement is claimed unless a before/after pair is measured.

## Environment overrides

```bash
PERF_API_URL=http://localhost:5082
WEBHOOK_SECRET=whsec_perf_local
PERF_RUN_ID=perf-load-002
PERF_THINK_TIME=1
PERF_PAGE_SIZE=25
PROMETHEUS_URL=http://localhost:9090
OTEL_SERVICE_NAME=ledgerline-api-perf
```

## Direct k6 command

The runner is preferred because it also samples infrastructure and checks consistency. To run a profile directly:

```bash
PERF_RUN_ID=perf-load-manual \
PERF_API_URL=http://localhost:5082 \
WEBHOOK_SECRET=whsec_perf_local \
PERF_TEST_TYPE=load \
k6 run perf/k6/load.js
```

## Reset the performance database

Only do this when you no longer need the current perf results/data:

```bash
docker compose -f docker-compose.perf.yml down -v
docker compose -f docker-compose.perf.yml up -d
```

The main demo database on `5432` is untouched.

## Safeguards

- Dedicated database: `payment_reconciliation_perf` on port `5433`.
- Demo seeder disabled for the perf API.
- Unique order references, payment ids and webhook ids per run.
- Duplicate webhook replay is intentional and isolated to the same run id.
- Consistency verifier fails if an order is overpaid, a provider payment id is duplicated, a matched payment is unlinked, an unmatched payment is linked, or a payment has no reconciliation record.
- Mock provider only; no real payment credentials or production data.
- No optimization was applied while producing the current baseline.
