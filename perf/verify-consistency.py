#!/usr/bin/env python3
"""Check order, payment and reconciliation consistency for a completed perf run."""

from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


def get_json(api_url: str, path: str, params: dict[str, str | int]) -> dict:
    query = urllib.parse.urlencode(params)
    request = urllib.request.Request(f"{api_url.rstrip('/')}{path}?{query}")
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        detail = error.read().decode(errors="replace")[:500]
        raise RuntimeError(f"GET {path} -> {error.code}: {detail}") from error


def fetch_all(api_url: str, path: str, search: str) -> list[dict]:
    items: list[dict] = []
    page = 1
    while True:
        response = get_json(api_url, path, {"search": search, "page": page, "pageSize": 100})
        items.extend(response.get("items", []))
        total_pages = int(response.get("totalPages", 1))
        if page >= total_pages or not response.get("items"):
            return items
        page += 1


def fetch_state(api_url: str, run_id: str) -> tuple[list[dict], list[dict], list[dict]]:
    return (
        fetch_all(api_url, "/api/orders", run_id),
        fetch_all(api_url, "/api/payments", run_id),
        fetch_all(api_url, "/api/reconciliation", run_id),
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api", default="http://localhost:5082")
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--timeout", type=float, default=15)
    parser.add_argument("--output", default=None)
    args = parser.parse_args()

    output = Path(args.output or f"perf/results/{args.run_id}-consistency.json")
    output.parent.mkdir(parents=True, exist_ok=True)

    deadline = time.time() + max(1, args.timeout)
    orders: list[dict] = []
    payments: list[dict] = []
    records: list[dict] = []

    while True:
        orders, payments, records = fetch_state(args.api, args.run_id)
        webhook_payments = [
            payment for payment in payments if payment["providerPaymentId"].startswith("perfwh-")
        ]
        webhook_ready = bool(webhook_payments) and all(
            payment["status"] != "Received" for payment in webhook_payments
        )
        if webhook_ready or time.time() >= deadline:
            break
        time.sleep(1)

    errors: list[str] = []
    successful_outcomes = {"Matched", "PartiallyMatched", "ManuallyMatched"}

    if not orders:
        errors.append("No orders were found for this run id.")
    if not payments:
        errors.append("No payments were found for this run id.")
    if not records:
        errors.append("No reconciliation records were found for this run id.")

    order_numbers = [order["orderNumber"] for order in orders]
    customer_refs = [order["customerReference"] for order in orders]
    if len(order_numbers) != len(set(order_numbers)):
        errors.append("Duplicate order numbers detected.")
    if len(customer_refs) != len(set(customer_refs)):
        errors.append("Duplicate customer references detected.")

    provider_payment_ids = [payment["providerPaymentId"] for payment in payments]
    if len(provider_payment_ids) != len(set(provider_payment_ids)):
        errors.append("Duplicate provider payment ids detected.")

    for order in orders:
        if order["paidAmount"] > order["expectedAmount"]:
            errors.append(f"Order {order['orderNumber']} is overpaid.")
        if order["status"] == "Paid" and order["paidAmount"] != order["expectedAmount"]:
            errors.append(f"Order {order['orderNumber']} is Paid but paid amount differs from expected.")

    records_by_payment: dict[str, list[dict]] = {}
    for record in records:
        records_by_payment.setdefault(record["paymentId"], []).append(record)

    for payment in payments:
        provider_id = payment["providerPaymentId"]
        related_records = records_by_payment.get(payment["id"], [])
        successes = [record for record in related_records if record["outcome"] in successful_outcomes]

        if provider_id.startswith(("perfpay-", "perfwh-", "perfseed-")) and payment["status"] != "Matched":
            errors.append(f"Payment {provider_id} is {payment['status']}, expected Matched.")
        if provider_id.startswith("perfunmatched-") and payment["status"] != "Unmatched":
            errors.append(f"Payment {provider_id} is {payment['status']}, expected Unmatched.")
        if provider_id.startswith("perfunmatched-") and payment["orderId"] is not None:
            errors.append(f"Unmatched payment {provider_id} is linked to an order.")
        if provider_id.startswith(("perfpay-", "perfwh-", "perfseed-")) and payment["orderId"] is None:
            errors.append(f"Matched payment {provider_id} has no linked order.")
        if len(successes) > 1:
            errors.append(f"Payment {provider_id} has more than one successful reconciliation record.")
        if not related_records:
            errors.append(f"Payment {provider_id} has no reconciliation record.")

    result = {
        "runId": args.run_id,
        "api": args.api.rstrip("/"),
        "checkedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "counts": {
            "orders": len(orders),
            "payments": len(payments),
            "reconciliationRecords": len(records),
        },
        "errors": errors,
        "passed": len(errors) == 0,
    }
    output.write_text(json.dumps(result, indent=2) + "\n")

    print(json.dumps(result["counts"], indent=2))
    if errors:
        print(f"Consistency check FAILED ({len(errors)} issue(s)); see {output}", file=sys.stderr)
        for error in errors[:20]:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Consistency check PASSED; report: {output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as error:
        print(f"Consistency check failed: {error}", file=sys.stderr)
        raise SystemExit(1)
