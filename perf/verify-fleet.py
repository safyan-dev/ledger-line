#!/usr/bin/env python3
"""Verify reconciliation invariants per tenant after a fleet load run.

Runs as a tenant owner (same host + bearer token), so every query is tenant-scoped
by the API itself rather than by a client-supplied id.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


SUCCESS_OUTCOMES = {"Matched", "PartiallyMatched", "ManuallyMatched"}


def login(base: str, tenant: dict) -> str:
    request = urllib.request.Request(
        f"{base.rstrip('/')}/api/auth/login",
        data=json.dumps({"email": tenant["email"], "password": tenant["password"]}).encode(),
        method="POST",
    )
    request.add_header("Host", tenant["host"])
    request.add_header("content-type", "application/json")

    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)["accessToken"]


def get(base: str, host: str, token: str, path: str, params: dict[str, str | int]) -> dict:
    query = urllib.parse.urlencode(params)
    request = urllib.request.Request(f"{base.rstrip('/')}{path}?{query}")
    request.add_header("Host", host)
    request.add_header("Authorization", f"Bearer {token}")

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        detail = error.read().decode(errors="replace")[:300]
        raise RuntimeError(f"GET {path} -> {error.code}: {detail}") from error


def fetch_all(base: str, host: str, token: str, path: str, search: str) -> list[dict]:
    items: list[dict] = []
    page = 1

    while True:
        response = get(base, host, token, path, {"search": search, "page": page, "pageSize": 100})
        items.extend(response.get("items", []))
        total_pages = int(response.get("totalPages", 1) or 1)
        if page >= total_pages or not response.get("items"):
            return items
        page += 1


def check_tenant(base: str, tenant: dict, run_id: str) -> tuple[list[str], int, int, int]:
    host = tenant["host"]
    slug = tenant["slug"]
    errors: list[str] = []

    # Sign in fresh: a long run may have rotated the seeded refresh token away.
    try:
        token = login(base, tenant)
    except (urllib.error.HTTPError, urllib.error.URLError) as error:
        return [f"{slug}: could not sign in ({error})"], 0, 0, 0

    orders = fetch_all(base, host, token, "/api/orders", run_id)
    payments = fetch_all(base, host, token, "/api/payments", run_id)
    records = fetch_all(base, host, token, "/api/reconciliation", run_id)

    order_numbers = [order["orderNumber"] for order in orders]
    if len(order_numbers) != len(set(order_numbers)):
        errors.append(f"{slug}: duplicate order numbers")

    if not orders:
        errors.append(f"{slug}: no orders were recorded for this run")
    if not payments:
        errors.append(f"{slug}: no payments were recorded for this run")

    customer_refs = [order["customerReference"] for order in orders if order.get("customerReference")]
    if len(customer_refs) != len(set(customer_refs)):
        errors.append(f"{slug}: duplicate customer references")

    provider_ids = [payment["providerPaymentId"] for payment in payments]
    if len(provider_ids) != len(set(provider_ids)):
        errors.append(f"{slug}: duplicate provider payment ids")

    for order in orders:
        if order["paidAmount"] > order["expectedAmount"]:
            errors.append(f"{slug}: order {order['orderNumber']} is overpaid")
        if order["status"] == "Paid" and order["paidAmount"] != order["expectedAmount"]:
            errors.append(f"{slug}: order {order['orderNumber']} is Paid with a partial amount")

    records_by_payment: dict[str, list[dict]] = {}
    for record in records:
        records_by_payment.setdefault(record["paymentId"], []).append(record)

    # perfrace pairs two settlements against one order: one wins and settles it,
    # the other is captured as Overpaid for review. Both must stay unmatched-to-order.
    race_payments = [payment for payment in payments if payment["providerPaymentId"].startswith("perfrace-")]
    matched_races = [payment for payment in race_payments if payment["status"] == "Matched"]
    overpaid_races = [
        payment
        for payment in race_payments
        if any(record["outcome"] == "Overpaid" for record in records_by_payment.get(payment["id"], []))
    ]
    if len(matched_races) != len(overpaid_races):
        errors.append(
            f"{slug}: {len(matched_races)} racing payments matched but {len(overpaid_races)} were captured as overpaid"
        )

    duplicated_payment_ids = [payment["providerPaymentId"] for payment in payments]
    duplicate_bursts = [pid for pid in duplicated_payment_ids if pid.startswith("perfdup-")]
    if len(duplicate_bursts) != len(set(duplicate_bursts)):
        errors.append(f"{slug}: duplicate payment burst created more than one payment")

    orders_by_id = {order["id"]: order for order in orders}
    for payment in payments:
        if not payment["providerPaymentId"].startswith("perfdup-"):
            continue
        order = orders_by_id.get(payment["orderId"])
        if order is None:
            errors.append(f"{slug}: duplicate burst payment {payment['providerPaymentId']} is not linked to its order")
            continue
        if order["status"] != "Paid" or order["paidAmount"] != order["expectedAmount"]:
            errors.append(
                f"{slug}: order {order['orderNumber']} was not settled exactly once by its duplicate burst "
                f"({order['status']}, paid {order['paidAmount']} of {order['expectedAmount']})"
            )

    for payment in payments:
        provider_id = payment["providerPaymentId"]
        related = records_by_payment.get(payment["id"], [])
        successes = [record for record in related if record["outcome"] in SUCCESS_OUTCOMES]

        if provider_id.startswith(("perfpay-", "perfwh-", "perfdup-")):
            if payment["status"] != "Matched":
                errors.append(f"{slug}: payment {provider_id} is {payment['status']}, expected Matched")
            if payment["orderId"] is None:
                errors.append(f"{slug}: matched payment {provider_id} has no linked order")
        if provider_id.startswith("perfrace-") and payment["status"] == "Matched" and payment["orderId"] is None:
            errors.append(f"{slug}: matched racing payment {provider_id} has no linked order")
        if provider_id.startswith("perfunmatched-"):
            if payment["status"] != "Unmatched":
                errors.append(f"{slug}: payment {provider_id} is {payment['status']}, expected Unmatched")
            if payment["orderId"] is not None:
                errors.append(f"{slug}: unmatched payment {provider_id} is linked to an order")
        if len(successes) > 1:
            errors.append(f"{slug}: payment {provider_id} has {len(successes)} successful reconciliation records")
        if not related:
            errors.append(f"{slug}: payment {provider_id} has no reconciliation record")

    return errors, len(orders), len(payments), len(records)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--fleet", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--api", default=None)
    parser.add_argument("--output", default=None)
    parser.add_argument("--concurrency", type=int, default=8)
    parser.add_argument(
        "--tenant-limit",
        type=int,
        default=0,
        help="Only verify the first N tenants (the ones the run targeted). 0 verifies all.",
    )
    args = parser.parse_args()

    fleet = json.loads(Path(args.fleet).read_text())
    base = args.api or fleet["api"]
    output = Path(args.output or f"perf/results/{args.run_id}-fleet-consistency.json")
    output.parent.mkdir(parents=True, exist_ok=True)

    tenants = fleet["tenants"]
    if args.tenant_limit > 0:
        tenants = tenants[: args.tenant_limit]

    errors: list[str] = []
    totals = {"tenants": 0, "orders": 0, "payments": 0, "reconciliationRecords": 0}

    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.concurrency)) as pool:
        futures = {
            pool.submit(check_tenant, base, tenant, args.run_id): tenant["slug"]
            for tenant in tenants
        }

        for future in concurrent.futures.as_completed(futures):
            slug = futures[future]
            try:
                tenant_errors, order_count, payment_count, record_count = future.result()
            except (RuntimeError, KeyError, json.JSONDecodeError) as error:
                tenant_errors = [f"{slug}: {error}"]
                order_count = payment_count = record_count = 0

            totals["tenants"] += 1
            totals["orders"] += order_count
            totals["payments"] += payment_count
            totals["reconciliationRecords"] += record_count
            errors.extend(tenant_errors)

    result = {
        "runId": args.run_id,
        "api": base,
        "checkedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "counts": totals,
        "errors": errors[:200],
        "errorCount": len(errors),
        "passed": len(errors) == 0,
    }
    output.write_text(json.dumps(result, indent=2) + "\n")

    print(json.dumps(totals, indent=2))
    if errors:
        print(f"Fleet consistency check FAILED ({len(errors)} issue(s)); see {output}", file=sys.stderr)
        for error in errors[:20]:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Fleet consistency check PASSED; report: {output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, KeyError, json.JSONDecodeError) as error:
        print(f"Fleet consistency check failed: {error}", file=sys.stderr)
        raise SystemExit(1)
