#!/usr/bin/env python3
"""Create predictable orders and optional payments in the dedicated perf database."""

from __future__ import annotations

import argparse
import json
import re
import sys
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


def api(api_url: str, method: str, path: str, payload: dict | None = None) -> dict:
    body = None if payload is None else json.dumps(payload).encode()
    request = urllib.request.Request(
        f"{api_url.rstrip('/')}{path}",
        data=body,
        method=method,
        headers={"content-type": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        detail = error.read().decode(errors="replace")[:500]
        raise RuntimeError(f"{method} {path} -> {error.code}: {detail}") from error


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api", default="http://localhost:5082")
    parser.add_argument("--run-id", default=f"perf-{datetime.now(timezone.utc):%Y%m%d%H%M%S}")
    parser.add_argument("--orders", type=int, default=100)
    parser.add_argument("--payments", type=int, default=0)
    parser.add_argument("--output", default=None)
    args = parser.parse_args()

    run_id = re.sub(r"[^A-Za-z0-9_-]", "-", args.run_id)
    output = Path(args.output or f"perf/results/{run_id}-seed.json")
    output.parent.mkdir(parents=True, exist_ok=True)

    orders: list[dict] = []
    payments: list[dict] = []

    for index in range(args.orders):
        amount = 100 + (index % 10)
        order = api(
            args.api,
            "POST",
            "/api/orders",
            {
                "customerName": f"Perf Seed Customer {index:04d}",
                "customerReference": f"PERF-{run_id}-SEED-{index:04d}",
                "description": "Deterministic performance seed order",
                "expectedAmount": amount,
                "currency": "USD",
            },
        )
        orders.append(order)

    for index in range(min(args.payments, len(orders))):
        order = orders[index]
        payment = api(
            args.api,
            "POST",
            "/api/payments",
            {
                "provider": "perfseed",
                "providerPaymentId": f"perfseed-{run_id}-{index:04d}",
                "amount": order["expectedAmount"],
                "currency": order["currency"],
                "customerName": order["customerName"],
                "customerReference": order["customerReference"],
                "reference": order["orderNumber"],
            },
        )
        payments.append(payment)

    state = {
        "runId": run_id,
        "api": args.api.rstrip("/"),
        "orders": orders,
        "payments": payments,
        "createdAt": datetime.now(timezone.utc).isoformat(),
    }
    output.write_text(json.dumps(state, indent=2) + "\n")

    print(f"Seeded {len(orders)} orders and {len(payments)} payments.")
    print(f"State: {output}")
    print(f"Use PERF_RUN_ID={run_id} for the matching performance test.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as error:
        print(f"Seed failed: {error}", file=sys.stderr)
        raise SystemExit(1)
