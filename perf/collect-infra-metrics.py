#!/usr/bin/env python3
"""Sample application/database metrics from Prometheus while a k6 test runs."""

from __future__ import annotations

import argparse
import csv
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


def instant(prometheus_url: str, expression: str) -> float | None:
    url = f"{prometheus_url.rstrip('/')}/api/v1/query?" + urllib.parse.urlencode({"query": expression})
    try:
        with urllib.request.urlopen(url, timeout=10) as response:
            payload = json.load(response)
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
        return None

    results = payload.get("data", {}).get("result", [])
    if not results:
        return None
    value = results[0].get("value", [None, None])[1]
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--prometheus", default="http://localhost:9090")
    parser.add_argument("--service", default="ledgerline-api-perf")
    parser.add_argument("--duration", type=float, default=90)
    parser.add_argument("--interval", type=float, default=5)
    parser.add_argument("--output", default="perf/results/infra.csv")
    args = parser.parse_args()

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    service = args.service.replace('"', "")

    queries = {
        "cpu_percent": (
            f'100 * sum(rate(dotnet_process_cpu_time_seconds_total{{service_name="{service}",cpu_mode=~"user|system"}}[1m]))'
        ),
        "memory_bytes": f'max(dotnet_process_memory_working_set_bytes{{service_name="{service}"}})',
        "db_pool_used": (
            f'sum(db_client_connection_count{{service_name="{service}",db_client_connection_state="used"}})'
        ),
        "db_pool_idle": (
            f'sum(db_client_connection_count{{service_name="{service}",db_client_connection_state="idle"}})'
        ),
        "db_pool_max": f'max(db_client_connection_max{{service_name="{service}"}})',
        "db_query_p95_seconds": (
            f'histogram_quantile(0.95, sum by (le) (db_client_operation_duration_seconds_bucket{{service_name="{service}"}}))'
        ),
        "http_p95_seconds": (
            f'histogram_quantile(0.95, sum by (le) (http_server_request_duration_seconds_bucket{{service_name="{service}",http_route!~"/health.*"}}))'
        ),
    }

    deadline = time.monotonic() + max(1, args.duration)
    warned = False
    with output.open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["timestamp", *queries.keys()])
        writer.writeheader()

        while time.monotonic() < deadline:
            timestamp = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            row = {"timestamp": timestamp}
            for name, expression in queries.items():
                row[name] = instant(args.prometheus, expression)

            if all(row[name] is None for name in queries):
                if not warned:
                    print("Prometheus has no perf-service metrics yet; writing empty samples.", file=sys.stderr)
                    warned = True
            else:
                writer.writerow(row)
                handle.flush()

            time.sleep(max(1, args.interval))

    print(f"Infrastructure samples: {output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        raise SystemExit(0)
    except urllib.error.URLError as error:
        print(f"Could not reach Prometheus: {error}", file=sys.stderr)
        raise SystemExit(1)
