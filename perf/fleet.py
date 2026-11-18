#!/usr/bin/env python3
"""Register a fleet of tenants, confirm their email and capture their webhook secret.

Each tenant is a real workspace owner with a confirmed account and a login session,
so k6 can sign in and drive full reconciliation workflows per tenant.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


PASSWORD = "Fleet-Passw0rd!"
RETRYABLE_STATUS = {429, 500, 502, 503, 504}
TOKEN_PATTERN = re.compile(r"^Token:\s*(\S+)\s*$", re.MULTILINE)
RECIPIENT_PATTERN = re.compile(r"^To:\s*(\S+)\s*$", re.MULTILINE)


class ApiError(RuntimeError):
    def __init__(self, status: int, path: str, detail: str) -> None:
        super().__init__(f"{path} -> {status}: {detail}")
        self.status = status


def request_json(
    base: str,
    method: str,
    path: str,
    payload: dict | None = None,
    headers: dict[str, str] | None = None,
    timeout: float = 30,
    retries: int = 6,
) -> dict | None:
    url = f"{base.rstrip('/')}{path}"
    body = None if payload is None else json.dumps(payload).encode()

    attempt = 0
    while True:
        attempt += 1
        request = urllib.request.Request(url, data=body, method=method)
        request.add_header("content-type", "application/json")
        for name, value in (headers or {}).items():
            request.add_header(name, value)

        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                raw = response.read()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as error:
            detail = error.read().decode(errors="replace")[:500]
            if error.code in RETRYABLE_STATUS and attempt <= retries:
                time.sleep(min(8.0, 0.5 * 2**attempt))
                continue
            raise ApiError(error.code, path, detail) from error
        except urllib.error.URLError as error:
            if attempt <= retries:
                time.sleep(min(8.0, 0.5 * 2**attempt))
                continue
            raise ApiError(0, path, str(error)) from error


def tenant_slug(run_id: str, index: int) -> str:
    return f"r{run_id}{index:05d}"


def normalize_run_id(value: str) -> str:
    cleaned = re.sub(r"[^a-z0-9]", "", value.lower())
    if not cleaned:
        raise SystemExit("run id must contain at least one letter or digit")
    return cleaned[:8]


def register_tenant(base: str, run_id: str, index: int, plan: str) -> dict:
    slug = tenant_slug(run_id, index)
    email = f"{slug}@perf.local"

    try:
        response = request_json(
            base,
            "POST",
            "/api/tenants/register",
            {
                "companyName": f"Fleet {slug}",
                "tenantSlug": slug,
                "ownerName": f"Fleet Owner {index}",
                "email": email,
                "password": PASSWORD,
                "planCode": plan,
            },
        )
    except ApiError as error:
        if error.status == 409:
            return {"slug": slug, "email": email, "created": False, "planCode": plan}
        raise

    tenant = (response or {}).get("tenant") or {}
    return {
        "slug": slug,
        "email": email,
        "tenantId": tenant.get("id"),
        "created": True,
        "planCode": (response or {}).get("planCode", plan),
    }


def collect_confirmation_tokens(outbox: Path, emails: set[str], since: float, timeout: float) -> dict[str, str]:
    pending = set(emails)
    found: dict[str, str] = {}
    deadline = time.monotonic() + timeout

    while pending and time.monotonic() < deadline:
        if outbox.is_dir():
            for entry in outbox.iterdir():
                if not entry.is_file():
                    continue
                try:
                    if entry.stat().st_mtime < since - 2:
                        continue
                    text = entry.read_text(errors="replace")
                except OSError:
                    continue

                recipient = RECIPIENT_PATTERN.search(text)
                token = TOKEN_PATTERN.search(text)
                if recipient is None or token is None:
                    continue

                email = recipient.group(1)
                if email in pending:
                    found[email] = token.group(1)
                    pending.discard(email)

        if pending:
            time.sleep(1)

    return found


def complete_tenant(base: str, record: dict, token: str | None, host_suffix: str) -> dict:
    slug = record["slug"]
    host = f"{slug}.{host_suffix}"
    headers = {"Host": host}

    if token:
        request_json(base, "POST", "/api/auth/confirm-email", {"email": record["email"], "token": token})

    session = request_json(
        base,
        "POST",
        "/api/auth/login",
        {"email": record["email"], "password": PASSWORD},
        headers=headers,
    )

    authorization = {"Authorization": f"Bearer {session['accessToken']}"}
    secret = request_json(
        base,
        "GET",
        "/api/tenants/current/webhook-secrets/mock",
        headers={**headers, **authorization},
    )

    return {
        **record,
        "host": host,
        "password": PASSWORD,
        "accessToken": session["accessToken"],
        "refreshToken": session["refreshToken"],
        "accessTokenExpiresAt": session["accessTokenExpiresAt"],
        "webhookSecret": (secret or {}).get("secret"),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api", default="http://127.0.0.1:5082")
    parser.add_argument("--run-id", default=f"fleet{int(time.time()) % 100000000:08d}")
    parser.add_argument("--count", type=int, default=50)
    parser.add_argument("--start-index", type=int, default=0)
    parser.add_argument("--concurrency", type=int, default=6)
    parser.add_argument("--plan", default="business")
    parser.add_argument("--host-suffix", default="localhost")
    parser.add_argument("--outbox", default="backend/src/Api/email-outbox")
    parser.add_argument("--output", default=None)
    parser.add_argument("--timeout", type=float, default=180)
    args = parser.parse_args()

    run_id = normalize_run_id(args.run_id)
    outbox = Path(args.outbox)
    output = Path(args.output or f"perf/results/{run_id}-fleet.json")
    output.parent.mkdir(parents=True, exist_ok=True)

    request_json(args.api, "GET", "/health/live")
    since = time.time()

    records: list[dict] = []
    failures: list[str] = []
    print(f"Registering {args.count} tenants as {run_id}-* on {args.api} ({args.plan} plan)...")

    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.concurrency)) as pool:
        futures = {
            pool.submit(register_tenant, args.api, run_id, args.start_index + offset, args.plan): offset
            for offset in range(args.count)
        }
        for completed, future in enumerate(concurrent.futures.as_completed(futures), start=1):
            try:
                records.append(future.result())
            except Exception as error:  # noqa: BLE001 - report and continue the fleet
                failures.append(str(error))
            if completed % 25 == 0 or completed == args.count:
                print(f"  registered {completed}/{args.count}")

    if not records:
        print("No tenants were registered.", file=sys.stderr)
        return 1

    emails = {record["email"] for record in records}
    created = sum(1 for record in records if record.get("created"))
    print(f"Collecting email confirmation tokens for {len(emails)} owners ({created} new tenants)...")
    tokens = collect_confirmation_tokens(outbox, emails, since, args.timeout)
    print(f"  found {len(tokens)}/{len(emails)} confirmation tokens")

    completed: list[dict] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.concurrency)) as pool:
        futures = {
            pool.submit(complete_tenant, args.api, record, tokens.get(record["email"]), args.host_suffix): record
            for record in records
        }
        for future in concurrent.futures.as_completed(futures):
            record = futures[future]
            try:
                completed.append(future.result())
            except Exception as error:  # noqa: BLE001 - keep the usable tenants
                failures.append(f"{record['slug']}: {error}")

    completed.sort(key=lambda record: record["slug"])
    without_secret = [record["slug"] for record in completed if not record.get("webhookSecret")]

    state = {
        "runId": run_id,
        "api": args.api.rstrip("/"),
        "hostSuffix": args.host_suffix,
        "planCode": args.plan,
        "tenants": completed,
        "failures": failures[:50],
        "createdAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    output.write_text(json.dumps(state, indent=2) + "\n")

    print(f"Fleet ready: {len(completed)} tenants with sessions ({len(without_secret)} missing a webhook secret)")
    print(f"State: {output}")
    print(f"Use PERF_RUN_ID={run_id} and FLEET_FILE={output} for the load test.")
    return 0 if completed else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ApiError as error:
        print(f"Fleet setup failed: {error}", file=sys.stderr)
        raise SystemExit(1)
