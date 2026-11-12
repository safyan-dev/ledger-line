#!/usr/bin/env bash
# Low-level helper for posting one signed webhook event.
# Prefer scripts/simulate-webhooks.mjs for scenarios, retries and verification.
#
# The event must reference an order via data.orderId.
#
# Usage:
#   ORDER_ID=<order uuid> ./scripts/send-webhook.sh
#   ORDER_ID=<order uuid> ./scripts/send-webhook.sh --same-event
#   API_URL=http://localhost:5080 ORDER_ID=<uuid> ./scripts/send-webhook.sh

set -euo pipefail

API_URL="${API_URL:-http://localhost:5080}"
SECRET="${WEBHOOK_SECRET:-whsec_dev_ledgerline}"
ORDER_ID="${ORDER_ID:?Set ORDER_ID to an order UUID (GET /api/orders)}"
EVENT_ID="${WEBHOOK_EVENT_ID:-evt_demo_$(date +%s)}"
PAYMENT_ID="${PAYMENT_ID:-pay_demo_$(date +%s)}"

if [[ "${1:-}" == "--same-event" ]]; then
  EVENT_ID="evt_demo_replay_001"
  PAYMENT_ID="pay_demo_replay_001"
fi

BODY=$(cat <<JSON
{"id":"${EVENT_ID}","type":"payment.succeeded","created":1762400000,"data":{"paymentId":"${PAYMENT_ID}","orderId":"${ORDER_ID}","amount":125000,"currency":"USD","reference":"ORD-2026-0001","customerName":"Northwind Traders","customerReference":"NW-5512"}}
JSON
)

SIGNATURE=$(printf '%s' "$BODY" | openssl dgst -sha256 -hmac "$SECRET" -hex | sed 's/.*= //')

echo "POST ${API_URL}/api/webhooks/mock (ack is asynchronous; poll GET /api/webhooks)"
echo "event: ${EVENT_ID}"

curl -sS -i -X POST "${API_URL}/api/webhooks/mock" \
  -H "content-type: application/json" \
  -H "X-Provider-Signature: sha256=${SIGNATURE}" \
  --data "$BODY"
