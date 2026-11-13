#!/usr/bin/env bash
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
backend="$root"
status=0

echo "Module boundary check: $backend/src/Modules"

for module_dir in "$backend"/src/Modules/*; do
  module=$(basename "$module_dir")

  if grep -RInE "using PaymentReconciliation\\.Modules\\.[A-Za-z]+\\.(Domain|Application|Infrastructure)" \
      --include='*.cs' "$module_dir" 2>/dev/null \
      | grep -v "/obj/" \
      | grep -v "Modules\\.$module\\." >/dev/null; then
    echo "FAIL: $module uses another module's internal namespace"
    grep -RInE "using PaymentReconciliation\\.Modules\\.[A-Za-z]+\\.(Domain|Application|Infrastructure)" \
      --include='*.cs' "$module_dir" | grep -v "/obj/" | grep -v "Modules\\.$module\\." || true
    status=1
  fi
done

check_entity() {
  entity="$1"
  owner="$2"

  for module_dir in "$backend"/src/Modules/*; do
    module=$(basename "$module_dir")
    [ "$module" = "$owner" ] && continue

    if grep -RIn "Set<$entity>(" --include='*.cs' "$module_dir" 2>/dev/null | grep -v "/obj/" >/dev/null; then
      echo "FAIL: $module reads Set<$entity> owned by $owner"
      grep -RIn "Set<$entity>(" --include='*.cs' "$module_dir" | grep -v "/obj/" || true
      status=1
    fi
  done
}

check_entity Order Orders
check_entity Payment Payments
check_entity Refund Payments
check_entity WebhookEvent Webhooks
check_entity ReconciliationRecord Reconciliation
check_entity AuditLog Auditing
check_entity Tenant Tenancy
check_entity TenantUser Tenancy

if grep -q '../Tenancy/Tenancy.csproj' "$backend/src/Modules/Identity/Identity.csproj"; then
  echo "FAIL: Identity references Tenancy"
  status=1
fi

if [ "$status" -eq 0 ]; then
  echo "OK: no boundary violations found"
fi

exit "$status"
