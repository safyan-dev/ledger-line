#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_DIR/backend/src/Api/Api.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "The .NET SDK is required but 'dotnet' was not found." >&2
  exit 1
fi

SECRETS="$(dotnet user-secrets list --project "$PROJECT" 2>/dev/null || true)"

get_existing() {
  printf '%s\n' "$SECRETS" | sed -n "s/^$1 = //p" | head -n 1
}

generate_hex() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex 32
  elif command -v python3 >/dev/null 2>&1; then
    python3 -c 'import secrets; print(secrets.token_hex(32))'
  elif [ -r /dev/urandom ]; then
    od -An -N32 -tx1 /dev/urandom | tr -d ' \n'
  else
    echo "Unable to generate a secret. Install openssl or python3." >&2
    exit 1
  fi
}

connection_string="${DB_CONNECTION:-$(get_existing "ConnectionStrings:Database")}"
if [ -z "$connection_string" ]; then
  connection_string="Host=localhost;Port=5432;Database=payment_reconciliation;Username=postgres;Password=postgres"
fi

jwt_signing_key="${JWT_SIGNING_KEY:-$(get_existing "Jwt:SigningKey")}"
if [ -z "$jwt_signing_key" ]; then
  jwt_signing_key="$(generate_hex)"
fi

webhook_secret="${WEBHOOK_SIGNING_SECRET:-$(get_existing "Webhooks:SigningSecret")}"
if [ -z "$webhook_secret" ]; then
  webhook_secret="whsec_$(generate_hex)"
fi

mock_secret="${WEBHOOK_MOCK_SECRET:-$(get_existing "Webhooks:Providers:mock:SigningSecret")}"
if [ -z "$mock_secret" ]; then
  mock_secret="whsec_$(generate_hex)"
fi

stripe_secret="${WEBHOOK_STRIPE_SECRET:-$(get_existing "Webhooks:Providers:stripe:SigningSecret")}"
if [ -z "$stripe_secret" ]; then
  stripe_secret="whsec_$(generate_hex)"
fi

demo_password="${DEMO_PASSWORD:-$(get_existing "Demo:Password")}"
if [ -z "$demo_password" ]; then
  demo_password="Demo123!ChangeMe"
fi

platform_password="${PLATFORM_PASSWORD:-$(get_existing "Demo:PlatformPassword")}"
if [ -z "$platform_password" ]; then
  platform_password="Platform123!ChangeMe"
fi

dotnet user-secrets set "ConnectionStrings:Database" "$connection_string" --project "$PROJECT" >/dev/null
dotnet user-secrets set "Jwt:SigningKey" "$jwt_signing_key" --project "$PROJECT" >/dev/null
dotnet user-secrets set "Webhooks:SigningSecret" "$webhook_secret" --project "$PROJECT" >/dev/null
dotnet user-secrets set "Webhooks:Providers:mock:SigningSecret" "$mock_secret" --project "$PROJECT" >/dev/null
dotnet user-secrets set "Webhooks:Providers:stripe:SigningSecret" "$stripe_secret" --project "$PROJECT" >/dev/null
dotnet user-secrets set "Demo:Password" "$demo_password" --project "$PROJECT" >/dev/null
dotnet user-secrets set "Demo:PlatformPassword" "$platform_password" --project "$PROJECT" >/dev/null

echo "Local user-secrets configured for backend/src/Api."
echo
echo "Demo owner:     avery.chen@ledgerline.local / $demo_password"
echo "Platform owner: platform@ledgerline.local / $platform_password"
echo "Tenant slug:    demo"
echo
echo "Inspect local secrets with:"
echo "  dotnet user-secrets list --project backend/src/Api"
