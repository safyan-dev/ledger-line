# Performance results

## Scope

This document records only measurements that were actually run. The current set contains one **baseline** run. Load, stress, spike and soak profiles are implemented but have not been executed after this baseline.

Baseline executed on 2026-11-12 against:

- Local API at `http://localhost:5082`
- Dedicated Postgres `payment_reconciliation_perf` on port `5433`
- k6 2.3.0
- 5 VUs, 1 minute
- Run id: `perf-baseline-demo`

The run created 580 orders, 870 payments and 1,160 reconciliation records. The post-run consistency check passed.

## k6 HTTP results

| Metric | Measured |
|---|---:|
| Requests | 3,190 |
| Throughput | 52.30 req/s |
| Average response | 4.45 ms |
| p50 response | 3.73 ms |
| p95 response | 11.42 ms |
| p99 response | 16.90 ms |
| Failed requests | 0.00% |
| Iterations | 290 |
| Max VUs | 5 |

Thresholds:

- `http_req_failed < 1%` — passed
- `http_req_duration p(95) < 500 ms` — passed
- `http_req_duration p(99) < 1000 ms` — passed

## Infrastructure samples

Sampled every 5 seconds from Prometheus for `service_name="ledgerline-api-perf"`:

| Metric | Average | Maximum |
|---|---:|---:|
| Application CPU | 7.13% | 14.54% |
| Working set memory | 241.84 MB | 323.19 MB |
| Npgsql pool used | 0 | 0 |
| Npgsql pool idle | 8.31 | 9 |
| Npgsql pool max | 100 | 100 |
| Database query p95 | 4.30 ms | 4.40 ms |
| HTTP p95 from OTel | 14.40 ms | 16.00 ms |

The pool stayed idle at the 5-second sample points; the run did not exhaust the pool. The database p95 stayed around 4.3 ms at this concurrency.

## Consistency check

`perf-baseline-demo-baseline-consistency.json` recorded:

- 580 orders
- 870 payments
- 1,160 reconciliation records
- No duplicate order numbers
- No duplicate provider payment ids
- No overpaid orders
- No matched payment without an order
- No unmatched payment linked to an order
- At least one reconciliation record per payment
- At most one successful reconciliation record per payment

## Bottlenecks and improvements

No bottleneck was demonstrated by this baseline alone. The useful next measurements are:

1. Run `load` and compare p95/p99, database p95 and pool usage with this baseline.
2. Run `stress` until error rate or latency degrades, then inspect which endpoint and database operation move first.
3. Run `soak` and compare memory and pool trends over time.

No optimization has been applied for this setup, so there is **no before/after performance claim** in this document. Any future improvement must use the same API, database, seed shape and k6 profile, with repeat runs, before it is called an improvement.

## Reproduce

```bash
docker compose -f docker-compose.perf.yml up -d
./perf/run-api.sh
python3 perf/seed.py --run-id perf-seed-001 --orders 100 --payments 100
./perf/run-test.sh baseline perf-baseline-002
```

Artifacts for the recorded baseline:

- `perf-baseline-demo-baseline-report.html`
- `perf-baseline-demo-baseline-summary.json`
- `perf-baseline-demo-baseline-infra.csv`
- `perf-baseline-demo-baseline-consistency.json`
