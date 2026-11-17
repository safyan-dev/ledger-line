#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
TYPE=${1:-baseline}
RUN_ID=${2:-perf-$(date +%Y%m%d%H%M%S)}
API_URL=${PERF_API_URL:-http://localhost:5082}
WEBHOOK_SECRET=${WEBHOOK_SECRET:-whsec_perf_local}
PROMETHEUS_URL=${PROMETHEUS_URL:-http://localhost:9090}
OTEL_SERVICE_NAME=${OTEL_SERVICE_NAME:-ledgerline-api-perf}
K6_BIN=${K6_BIN:-k6}

case "$TYPE" in
  baseline) DURATION=90 ;;
  load) DURATION=330 ;;
  stress) DURATION=660 ;;
  spike) DURATION=180 ;;
  soak) DURATION=1860 ;;
  *)
    echo "Unknown test type: $TYPE" >&2
    echo "Use one of: baseline, load, stress, spike, soak" >&2
    exit 2
    ;;
esac

if ! command -v "$K6_BIN" >/dev/null 2>&1; then
  echo "k6 was not found. Install it with: brew install k6" >&2
  echo "Then rerun: $0 $TYPE" >&2
  exit 2
fi

if ! curl -fsS "$API_URL/health/live" >/dev/null; then
  echo "Perf API is not reachable at $API_URL/health/live" >&2
  echo "Start it with: ./perf/run-api.sh" >&2
  exit 2
fi

mkdir -p "$REPO_DIR/perf/results"
cd "$REPO_DIR"

export PERF_RUN_ID="$RUN_ID"
export PERF_API_URL="$API_URL"
export WEBHOOK_SECRET="$WEBHOOK_SECRET"
export PERF_TEST_TYPE="$TYPE"

COLLECTOR_PID=""
if command -v python3 >/dev/null 2>&1; then
  python3 perf/collect-infra-metrics.py \
    --prometheus "$PROMETHEUS_URL" \
    --service "$OTEL_SERVICE_NAME" \
    --duration "$DURATION" \
    --interval 5 \
    --output "perf/results/${RUN_ID}-${TYPE}-infra.csv" &
  COLLECTOR_PID=$!
fi

cleanup() {
  if [ -n "$COLLECTOR_PID" ]; then
    kill "$COLLECTOR_PID" >/dev/null 2>&1 || true
    wait "$COLLECTOR_PID" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT INT TERM

echo "Running $TYPE with run id $RUN_ID"
echo "API: $API_URL"
echo "Reports: perf/results/${RUN_ID}-${TYPE}-report.html"

set +e
"$K6_BIN" run \
  -e "PERF_RUN_ID=$RUN_ID" \
  -e "PERF_API_URL=$API_URL" \
  -e "WEBHOOK_SECRET=$WEBHOOK_SECRET" \
  -e "PERF_TEST_TYPE=$TYPE" \
  "perf/k6/${TYPE}.js"
K6_EXIT=$?
set -e

cleanup
trap - EXIT INT TERM

VERIFY_EXIT=0
if command -v python3 >/dev/null 2>&1; then
  echo
  echo "Checking order/payment/reconciliation consistency..."
  python3 perf/verify-consistency.py \
    --api "$API_URL" \
    --run-id "$RUN_ID" \
    --output "perf/results/${RUN_ID}-${TYPE}-consistency.json" || VERIFY_EXIT=$?
fi

echo
echo "Artifacts:"
echo "  perf/results/${RUN_ID}-${TYPE}-report.html"
echo "  perf/results/${RUN_ID}-${TYPE}-summary.json"
echo "  perf/results/${RUN_ID}-${TYPE}-infra.csv"
echo "  perf/results/${RUN_ID}-${TYPE}-consistency.json"

if [ "$K6_EXIT" -ne 0 ]; then
  exit "$K6_EXIT"
fi
exit "$VERIFY_EXIT"
