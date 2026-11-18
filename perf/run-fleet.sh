#!/usr/bin/env sh
# Run the fleet load test while the stack is observed live.
# k6 web dashboard: http://127.0.0.1:5665   Grafana: http://localhost:3000   Jaeger: http://localhost:16686
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)

API_URL=${PERF_API_URL:-http://127.0.0.1:5082}
RUN_ID=${PERF_RUN_ID:-fleet-$(date +%Y%m%d%H%M%S)}
TARGET_VUS=${PERF_TARGET_VUS:-20}
PROMETHEUS_URL=${PROMETHEUS_URL:-http://localhost:9090}
OTEL_SERVICE_NAME=${OTEL_SERVICE_NAME:-ledgerline-api-perf}
K6_BIN=${K6_BIN:-k6}
DASHBOARD_PORT=${PERF_DASHBOARD_PORT:-5665}
DURATION_SECONDS=${PERF_DURATION_SECONDS:-180}

FLEET_FILE=${FLEET_FILE:-perf/results/${RUN_ID}-fleet.json}

if ! command -v "$K6_BIN" >/dev/null 2>&1; then
  echo "k6 was not found. Install it with: brew install k6" >&2
  exit 2
fi

if ! curl -fsS "$API_URL/health/live" >/dev/null; then
  echo "Perf API is not reachable at $API_URL/health/live" >&2
  echo "Start it with: ./perf/run-api.sh" >&2
  exit 2
fi

if [ ! -f "$FLEET_FILE" ]; then
  echo "Fleet file $FLEET_FILE not found." >&2
  echo "Create it first: python3 perf/fleet.py --run-id $RUN_ID --count $TARGET_VUS" >&2
  exit 2
fi

mkdir -p "$REPO_DIR/perf/results"
cd "$REPO_DIR"

# k6 resolves open() against the script directory, so hand it an absolute path.
case "$FLEET_FILE" in
  /*) ;;
  *) FLEET_FILE="$REPO_DIR/$FLEET_FILE" ;;
esac

export PERF_RUN_ID="$RUN_ID"
export PERF_API_URL="$API_URL"
export PERF_TEST_TYPE="fleet"
export FLEET_FILE
export PERF_TARGET_VUS="$TARGET_VUS"

COLLECTOR_PID=""
if command -v python3 >/dev/null 2>&1; then
  python3 perf/collect-infra-metrics.py \
    --prometheus "$PROMETHEUS_URL" \
    --service "$OTEL_SERVICE_NAME" \
    --duration "$((DURATION_SECONDS + 60))" \
    --interval 5 \
    --output "perf/results/${RUN_ID}-fleet-infra.csv" &
  COLLECTOR_PID=$!
fi

cleanup() {
  if [ -n "$COLLECTOR_PID" ]; then
    kill "$COLLECTOR_PID" >/dev/null 2>&1 || true
    wait "$COLLECTOR_PID" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT INT TERM

echo "Fleet run: $RUN_ID"
echo "  api           $API_URL"
echo "  fleet         $FLEET_FILE ($TARGET_VUS tenants)"
echo "  live k6       http://127.0.0.1:$DASHBOARD_PORT"
echo "  grafana       http://localhost:3000"
echo "  jaeger        http://localhost:16686"
echo "  prometheus    $PROMETHEUS_URL"
echo

K6_EXIT=0
K6_WEB_DASHBOARD=true \
K6_WEB_DASHBOARD_PORT="$DASHBOARD_PORT" \
K6_WEB_DASHBOARD_EXPORT="perf/results/${RUN_ID}-fleet-dashboard.html" \
K6_PROMETHEUS_RW_SERVER_URL="${PROMETHEUS_RW_URL:-http://localhost:9090/api/v1/write}" \
K6_PROMETHEUS_RW_TREND_STATS="p(95),p(99),avg,max" \
"$K6_BIN" run \
  -e "PERF_RUN_ID=$RUN_ID" \
  -e "PERF_API_URL=$API_URL" \
  -e "PERF_TEST_TYPE=fleet" \
  -e "FLEET_FILE=$FLEET_FILE" \
  --summary-export "perf/results/${RUN_ID}-fleet-summary.json" \
  --out experimental-prometheus-rw \
  perf/k6/fleet.js || K6_EXIT=$?

cleanup
trap - EXIT INT TERM

echo
echo "Artifacts:"
echo "  perf/results/${RUN_ID}-fleet-dashboard.html"
echo "  perf/results/${RUN_ID}-fleet-summary.json"
echo "  perf/results/${RUN_ID}-fleet-infra.csv"

VERIFY_EXIT=0
if command -v python3 >/dev/null 2>&1; then
  echo
  echo "Checking per-tenant consistency..."
  python3 perf/verify-fleet.py \
    --fleet "$FLEET_FILE" \
    --run-id "$RUN_ID" \
    --tenant-limit "$TARGET_VUS" \
    --output "perf/results/${RUN_ID}-fleet-consistency.json" || VERIFY_EXIT=$?
fi

if [ "$K6_EXIT" -ne 0 ]; then
  echo
  echo "k6 exited with $K6_EXIT (thresholds or script errors)." >&2
  exit "$K6_EXIT"
fi

exit "$VERIFY_EXIT"
