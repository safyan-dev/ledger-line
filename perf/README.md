# Performance testing

This directory contains a k6 harness for the dedicated performance database. It does not touch the demo database and does not use real provider credentials.

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

## Run a profile

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
WEBHOOK_SECRET=whsec_dev_ledgerline
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
WEBHOOK_SECRET=whsec_dev_ledgerline \
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
