#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5082
export ConnectionStrings__Database="Host=localhost;Port=5433;Database=payment_reconciliation_perf;Username=postgres;Password=postgres"
export Database__SeedDemoData=false
export RateLimiting__AuthPermitLimit="${RATE_LIMIT_AUTH_PERMIT_LIMIT:-10000}"
export Jwt__SigningKey="${JWT_SIGNING_KEY:-perf-only-ledgerline-signing-key-change-me-32+chars}"
export Webhooks__SigningSecret="${WEBHOOK_SECRET:-whsec_perf_local}"
export Webhooks__Providers__mock__SigningSecret="${WEBHOOK_SECRET:-whsec_perf_local}"
export OTEL_SERVICE_NAME="${OTEL_SERVICE_NAME:-ledgerline-api-perf}"
export Observability__OtlpEndpoint="${Observability__OtlpEndpoint:-http://localhost:4317}"

echo "Starting perf API on http://localhost:5082"
echo "Database: payment_reconciliation_perf (port 5433)"
echo "OTel service: $OTEL_SERVICE_NAME"
echo "Demo seed disabled; run perf/seed.py for predictable data."
echo

cd "$REPO_DIR/backend"
exec dotnet run --project src/Api/Api.csproj --no-launch-profile
