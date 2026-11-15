# Ledgerline — Payment Reconciliation

A portfolio-scale payment reconciliation workspace for business operations and finance teams.

Orders carry an expected amount, provider webhooks record payments, a deterministic matcher links them, and every business action is written to an append-only audit log. Payments that cannot be matched with confidence land in a review queue where an operator can link them manually.

```
Provider webhook ──▶ Webhook event ──▶ Payment ──▶ Matcher ──▶ Order allocation
                          │                          │
                          └──────────────┬───────────┴──▶ Reconciliation record
                                         └──────────────▶ Audit log
```

## Stack

| Layer | Choice |
|---|---|
| API | .NET 10 minimal APIs, modular monolith, custom CQRS dispatcher, in-memory domain events |
| Persistence | PostgreSQL 17, EF Core 10, one schema per module, snake_case identifiers |
| Responses | Result pattern end-to-end (`Result<T>`) mapped to `ProblemDetails` |
| Client | React 19, TypeScript, Vite, Tailwind CSS v4, shadcn/ui (New York, slate), TanStack Query, React Router 7 |
| Testing | xUnit + Testcontainers (PostgreSQL) |

## Getting started

Prerequisites: .NET 10 SDK, Node 22+, Docker.

```bash
# 1. Database, pgAdmin and the observability stack
docker compose up -d
# Postgres :5432  ·  pgAdmin http://localhost:5050
# OTLP :4317/:4318  ·  Grafana http://localhost:3000  ·  Prometheus :9090  ·  Jaeger :16686

# 2. API — applies migrations, seeds demo data, and exports OTLP to localhost:4317
cd backend
dotnet build
dotnet run --project src/Api --launch-profile http
# http://localhost:5080  ·  API docs at /scalar
# health: /health · /health/live · /health/ready

# 3. Client
cd ../web
npm install
npm run dev
# http://localhost:5173  (Vite proxies /api to :5080)
```

The seeder only runs when the `orders` table is empty. To start from a clean slate:

```bash
docker compose down -v && docker compose up -d
cd backend && dotnet run --project src/Api --launch-profile http
```

### Database GUI (pgAdmin)

`docker compose up -d` also starts pgAdmin at **http://localhost:5050**.

| Field | Value |
|---|---|
| Login | `admin@ledgerline.dev` / `admin` |
| Server | *Ledgerline · Postgres* — pre-registered from `docker/pgadmin/servers.json` |
| Postgres user / password | `postgres` / `postgres` |
| Database | `payment_reconciliation` |

pgAdmin cannot import passwords from `servers.json`, so enter `postgres` on the first connection and tick **Save password**; it is persisted in the `pgadmin-data` volume. `docker compose down -v` clears pgAdmin settings and saved connections along with the database.

Useful starter query:

```sql
select o.order_number, o.status, o.expected_amount, o.paid_amount,
       count(p.id) as payments, sum(p.amount) as recorded
from orders.orders o
left join payments.payments p on p.order_id = o.id
group by o.id
order by o.created_at desc;
```

## Seeded demo

- Tenant: **demo** (`demo` slug, `tenancy.tenants`).
- Tenant owner: **Avery Chen** (`avery.chen@ledgerline.local`), an ASP.NET Core Identity user with an Owner `TenantUser` membership. Sign in at `demo.localhost:5173` with `Demo123!ChangeMe`.
- Platform/SaaS owner: **Platform Owner** (`platform@ledgerline.local`), an Identity user with `IsPlatformAdmin = true` and no tenant membership. Sign in at the root domain (no workspace subdomain) with `Platform123!ChangeMe`.
- Passwords come from `Demo:Password` and `Demo:PlatformPassword` in `appsettings.Development.json`, with the values above as defaults. Change them outside local development.
- 6 orders across every status (pending, partial, paid, overpaid, cancelled).
- 14 payments, including unmatched ones for the review queue.
- 12 webhook events (`mock` and `stripe`) with processed, failed and ignored outcomes.
- Reconciliation attempts for every payment, plus one manual match.
- Audit rows for the recent business actions.

All seeded business rows belong to the demo tenant.

## Authentication and tenancy

Authentication and tenancy are separate modules:

- **Identity** owns credentials, sessions, tokens, password reset and the `/api/auth/*` endpoints. It resolves the requested workspace through a small `ITenantMembershipVerifier` contract implemented by Tenancy; it never references the Tenancy module directly.
- **Tenancy** owns tenants, memberships, tenant resolution, plans and subscriptions. It depends on Identity, never the other way round.

The JWT identifies a **user only** (`sub`, `email`, `display_name`, `platform_admin`). It carries no tenant claim, so one token works on every tenant the user belongs to.

Login is credential-based and workspace-aware:

- A **platform admin** can sign in without a workspace slug and is routed to `/platform`.
- A **tenant user** must present the workspace slug. The web client derives it from the subdomain (`demo.localhost`); direct API calls can pass `tenantSlug` in the body. The backend verifies membership in that tenant before issuing tokens.
- A tenant user signing in without a slug gets `403 auth.tenant_required`; a user signing into a workspace they do not belong to gets `403 auth.tenant_membership_required`.

```bash
# Tenant owner: workspace slug required
curl -s -X POST localhost:5080/api/auth/login \
  -H 'content-type: application/json' \
  -d '{"email":"avery.chen@ledgerline.local","password":"Demo123!ChangeMe","tenantSlug":"demo"}'

# Platform owner: no workspace slug
curl -s -X POST localhost:5080/api/auth/login \
  -H 'content-type: application/json' \
  -d '{"email":"platform@ledgerline.local","password":"Platform123!ChangeMe"}'
```

Tenant APIs require a JWT **and** a resolvable tenant. Health checks, `POST /api/webhooks/{provider}`, `/api/auth/*`, `/api/platform/*` and `/api/tenants/register` are tenant-agnostic; platform endpoints additionally require the `PlatformAdmin` policy.

| Route | Module | Purpose |
|---|---|---|
| `POST /api/auth/register` | Identity | Create a user + tokens |
| `POST /api/auth/login` | Identity | Email + password + optional `tenantSlug` → tokens; slug is required for tenant users and optional for platform admins |
| `POST /api/auth/refresh` | Identity | Rotate refresh token |
| `POST /api/auth/logout` | Identity | Revoke refresh token |
| `GET /api/auth/me` | Identity | Current user |
| `POST /api/auth/change-password` | Identity | Change password, revokes sessions |
| `POST /api/auth/forgot-password` | Identity | Always `200`; dev response includes the reset token |
| `POST /api/auth/reset-password` | Identity | Consume reset token, revokes sessions |
| `POST /api/tenants/register` | Tenancy | Create tenant + owner membership + tokens |
| `GET /api/tenants/mine` | Tenancy | Current user's memberships |
| `GET /api/platform/tenants` | Tenancy | Platform admin: list workspaces (no tenant context) |

Plans, features and limits are database-backed and seeded idempotently at startup. New tenants get Free by default; a selected Growth/Business plan is stored as a simulated activation with `paymentConfirmed = false` because no payment is collected. Subscription changes write history and audit rows; an hourly worker applies due downgrades and expires due subscriptions without deleting tenant data.

### Roles, team and personal account

Authorization is centralized in `TenantPermission` + `TenantRolePermissions`; endpoints and handlers both enforce it, independently of the frontend.

| Role | Capabilities |
|---|---|
| Owner | Full tenant access, team and role management, workspace settings, subscription changes, usage, all business modules, support tickets |
| TenantAdmin | Team (non-owner) and role management, invitations, workspace settings, read subscription/usage, business modules, support tickets |
| Finance | Read/run operational modules, read subscription/usage, personal account, support tickets; no team, workspace or subscription changes |

- `POST /api/tenants/current/members` still adds an existing registered user directly; invitations are the email-based path:
  `POST /api/tenants/current/invitations`, `GET .../invitations`, `POST .../{id}/resend`, `POST .../{id}/revoke`, and `POST /api/invitations/accept`.
- Invitation tokens are single-use, 7-day, stored only as SHA-256 hashes, and scoped to the tenant in the token. Existing accounts must sign in to accept; unregistered emails create an account during acceptance. Owner invitations are not allowed.
- Personal account: `GET /api/account/profile`, `PATCH /api/account/profile`, `POST /api/account/change-email`, `POST /api/account/change-email/confirm`; password change, forgot/reset and logout remain under `/api/auth/*`. Email-change and password tokens are single-use and expire after one hour; password changes, email changes and logout revoke refresh tokens. Forgot-password never reveals whether an email exists.
- Email delivery is a logging sender in this build (recipient/subject only); invitation, reset and email-change tokens are returned in Development responses for testability.

### Usage and limits

`GET /api/usage` reports plan, period and `used / limit / remaining` for orders, payments and active team members, plus included features. `GET /api/usage/features/{key}` checks a feature. Limits are enforced in the backend on order creation, manual payment creation, webhook payment creation, direct member add and invitation acceptance. Enforcement takes a PostgreSQL transaction-scoped advisory lock per tenant/metric, then counts and inserts in the same transaction, so concurrent requests cannot bypass the limit.

A webhook payment over the payments limit is retained as a `Failed` event with a `plan.limit_reached` reason and no payment row; `POST /api/webhooks/{id}/reprocess` retries it after an upgrade or freed capacity. Downgrades and expiry never delete existing rows; they can leave usage above the new limit while blocking new creates.

### Workspace settings and support

- `GET/PUT /api/tenants/current` reads/updates workspace name, business contact email, default currency and time zone (Owner/TenantAdmin).
- `POST/GET /api/support/tickets`, `GET /api/support/tickets/{id}`, `POST /api/support/tickets/{id}/messages`, `PATCH /api/support/tickets/{id}/status`. All tenant roles can create, view and reply; only the Owner changes ticket status. Status transitions are validated, and tenant isolation is enforced by query filters plus a composite tenant/ticket foreign key.

Tenant resolution runs per request:

1. The tenant subdomain (`acme.localhost`) is resolved to a tenant. In Development only, `X-Tenant-Slug` is accepted as a fallback for tooling.
2. The server loads the `TenantUser` membership for the authenticated user and rejects non-members with `403`.
3. An unknown subdomain is `404`; a suspended or canceled tenant is `403`.
4. Tenant ids in request bodies or query strings are ignored.
5. No `/api/auth/switch-tenant` endpoint exists any more: switching tenants is a navigation, not a token exchange.

Webhook ingress resolves tenant from the subdomain (or, in Development only, `X-Tenant-Slug`); the payload cannot choose a tenant.

Isolation is enforced twice:

- EF Core global query filters on `Order`, `Payment`, `Refund`, `WebhookEvent`, `ReconciliationRecord`, and `AuditLog`.
- PostgreSQL `tenant_id` foreign keys and composite `(tenant_id, …)` relationships, so cross-tenant references fail at the database.

New tenant-owned rows are stamped with the server-resolved tenant and cannot be moved to another tenant.

### Tests

```bash
cd backend
dotnet test tests/PaymentReconciliation.Tests/PaymentReconciliation.Tests.csproj
```

The suite covers unauthenticated access, login/refresh/logout and token rotation, password reset (single use), change-password/email session revocation, the role permission matrix, last-owner protection, invitation creation/accept/resend/revoke/expiry, cross-tenant read and write attempts on every new endpoint, subscription lifecycle (upgrade/downgrade/cancel/reactivate/history), plan-limit enforcement including concurrent order creation and webhook fail/reprocess, workspace settings isolation, and support-ticket isolation. Tests use a PostgreSQL Testcontainer.

Platform administration, custom roles, real payment processing and frontend account/team screens are outside this scope.

## Trying the webhook pipeline

`POST /api/webhooks/{provider}` is a **fast ingress** endpoint: it verifies the HMAC signature, enqueues the raw body in a bounded in-memory queue, and returns `202` in under 5 ms (server-side). Persistence, parsing and payment/reconciliation work happen asynchronously in a hosted worker.

The simulator signs events, retries when the acknowledgement is missing, and verifies the asynchronous outcome through the API:

```bash
node scripts/simulate-webhooks.mjs --scenario e2e     # E2E-01..E2E-06
node scripts/simulate-webhooks.mjs --scenario all     # E2E + technical edge cases
node scripts/simulate-webhooks.mjs --scenario concurrent --iterations 20
node scripts/simulate-webhooks.mjs --api http://localhost:5080 --secret whsec_dev_ledgerline
```

`--scenario e2e` runs, in order:

| Case | What it proves |
|---|---|
| E2E-01 | normal payment → one order/payment/webhook, Matched record, order Paid, dashboard counts +1 |
| E2E-02 | the same webhook delivered four times → one payment, one order update, one `payment.recorded` audit row, `duplicateCount = 3` |
| E2E-03 | $90 against a $100 order → payment retained, `AmountMismatch`, order untouched, visible in the mismatch view and review queue |
| E2E-04 | payment for an unknown order id → payment retained as `Unmatched`, no order created or updated |
| E2E-05 | simulated processing failure (Development-only `X-Simulate-Processing-Failure` header), then the same event id retried → one payment, order Paid, `webhook.failed` + `webhook.processed` audit trail, `attemptCount ≥ 2` |
| E2E-06 | payment referencing `ORD-2001` before the order exists → Unmatched; create the order, re-run reconciliation → Matched/Paid with both history records retained. If `ORD-2001` already exists, the simulator uses a suffixed order number for repeatability. |

`--scenario all` adds concurrent delivery, mutated conflict, unknown type, missing identifiers, invalid payload, bad signature and failed-payment. The simulator creates E2E/TECH orders and payments and leaves them in place for inspection; use `--order-number` to override the E2E-06 order number.

`--timeout-ms` is the client network timeout (default **1000 ms**) for receiving an ack; `--ack-budget-ms` is the server-side budget (default **5 ms**) read from `X-Webhook-Ack-Ms`. A missing response, `5xx`/`503`, or a server ack over budget retries with the same event id (up to `--retries`). E2E-05 deliberately fails the first processing attempt with the Development-only `X-Simulate-Processing-Failure` header, then resends the same event id so the claim store can retry the `Failed` event.

Payload contract (`amount` is in minor units; `orderId` is the internal order GUID when known):

```json
{
  "id": "evt_01HXYZ",
  "type": "payment.succeeded",
  "created": 1762400000,
  "data": {
    "paymentId": "pay_01HXYZ",
    "orderId": "0f28e0ca-09ec-414c-a74d-9fddb9594a70",
    "amount": 125000,
    "currency": "USD",
    "reference": "ORD-2026-0001",
    "customerName": "Northwind Traders",
    "customerReference": "NW-5512"
  }
}
```

`type` is `payment.succeeded` or `payment.failed` (add `data.failureReason` for failures). Anything else is ignored. At least one of `orderId`, `reference` or `customerReference` is required. A payment that arrives **before** its order is accepted with a reference (no `orderId`), stored as `Unmatched`, and stays visible in the review queue; once the order exists, `POST /api/payments/{id}/reconcile` matches it.

| Situation | HTTP | Stored | Outcome |
|---|---|---|---|
| Valid signature, queue accepted | `202` | async | Worker processes |
| Duplicate event id, same payload | `202` | existing row only | discarded, `duplicateCount++` |
| Same event id, different payload | `202` | existing row | `conflictCount++`, `lastConflictReason` |
| Unknown event type | `202` | `Ignored` | no Payment/Order/Reconciliation |
| Order not found / payment before order | `202` | `Processed` | Payment `Unmatched` + `Unmatched` record |
| No `orderId`, `reference` or `customerReference` / invalid fields / malformed JSON | `202` | `Failed` | no payment; raw payload retained |
| `payment.failed` | `202` | `Processed` | Payment `Failed`, order untouched |
| Invalid / missing signature | `401` | **no row** | rejected before queueing |
| Queue full | `503` + `Retry-After` | no | retry with the same event id |
| Body > 1 MB | `413` | no | — |

The endpoint never returns processing errors to the provider. Duplicates are discarded via the unique `(provider, provider_event_id)` index and the worker's claim insert, which also handles simultaneous deliveries. A mutated replay (same event id, different payload) and a changed amount for the same provider payment are recorded as conflicts. Distinct payments for one order remain valid installments.

`X-Webhook-Ack-Ms` / `Server-Timing` report the server-side ack duration; the simulator asserts it stays within the 5 ms budget.

> Durability note: the ack happens before the database write. A process crash or shutdown can lose queued events; a webhook that is stored but stays `Received/Processing` for 5 minutes is marked `Failed` by a recovery sweep. A production deployment would use an outbox or broker.

## Observability

The API exports OpenTelemetry traces and metrics over OTLP to the collector; the collector exposes Prometheus metrics and forwards traces to Jaeger. Grafana is provisioned with the **Ledgerline · Payment Operations** dashboard and Prometheus/Jaeger datasources.

| Service | URL | Credentials |
|---|---|---|
| Grafana | http://localhost:3000 | `admin` / `admin` |
| Prometheus | http://localhost:9090 | — |
| Jaeger | http://localhost:16686 | — |
| Collector metrics | http://localhost:8889/metrics | — |

The API defaults to `http://localhost:4317`; override with `OTEL_EXPORTER_OTLP_ENDPOINT` or `Observability:OtlpEndpoint`. Set `OTEL_SERVICE_NAME` to change the service label.

### Instrumentation

- **Traces:** ASP.NET Core requests, HTTP clients, Npgsql commands, plus `payment.apply`, `webhook.process`, and `reconciliation.process` spans. The HTTP trace includes the route, status, and correlation ID; webhook/reconciliation spans include the provider, payment, order, and webhook event IDs.
- **API metrics:** request count/rate, status codes, p50/p95/p99 duration, and requests split by route from the ASP.NET Core instrumentation.
- **Business metrics:** successful/failed payments, webhooks received/processed/ignored, duplicate webhooks, webhook processing failures and duration, matched/unmatched/amount-mismatch outcomes, and reconciliation duration.
- **Infrastructure:** Npgsql connection pool counts/max and database operation duration; .NET runtime CPU, memory, GC, thread-pool, and exception metrics.
- **Logs:** structured JSON console logs include scopes for `CorrelationId`, `TraceId`, `PaymentId`, `OrderId`, and `WebhookEventId` where applicable. `X-Correlation-ID` is accepted on ingress and returned on responses. Request bodies, card data, secrets, and customer PII are never logged.

Metric names are standard Prometheus translations of the OpenTelemetry instruments, for example `http_server_request_duration_seconds_bucket`, `ledgerline_payments_successful_total`, `ledgerline_webhooks_duplicates_ignored_total`, `ledgerline_reconciliation_amount_mismatches_total`, `db_client_connection_count`, and `dotnet_process_memory_working_set_bytes`.

### Dashboard

The provisioned dashboard has five sections:

1. API health, request rate, error rate, latency percentiles, and per-route traffic.
2. Payment processing activity (successful and failed).
3. Webhook ingress, duplicate suppression, failures, and processing latency.
4. Reconciliation outcomes and processing latency.
5. Database pool/query health plus application CPU and memory.

Dashboard source: `docker/grafana/dashboards/ledgerline-observability.json`. Collector, Prometheus, and datasource configuration live under `docker/otel`, `docker/prometheus`, and `docker/grafana/provisioning`.

## Performance testing

A k6 harness lives under [`perf/`](./perf/README.md). It uses a dedicated Postgres database on port `5433`, a separate API on `5082`, predictable seed data, signed mock webhooks, five test profiles, HTML reports, infrastructure sampling and post-run consistency checks. The measured baseline and its limitations are in [`perf/results/RESULTS.md`](./perf/results/RESULTS.md).

## Architecture

### Modules

| Module | Owns | Public surface |
|---|---|---|
| Identity | ASP.NET Core Identity users, refresh tokens | auth contracts |
| Tenancy | Tenant, TenantUser, memberships, JWT token issue, tenant resolution | auth + tenant DTOs |
|---|---|---|
| Orders | `Order` aggregate, allocation, status transitions, order numbers | `OrderDto`, `IOrdersFacade`, order events |
| Payments | `Payment` aggregate, provider idempotency, matched/unmatched transitions | `PaymentDto`, `IPaymentsFacade`, payment events |
| Webhooks | `WebhookEvent`, signature verification, payload parsing, async ingress + worker | webhook DTOs, ingestion queue, processing commands |
| Reconciliation | `ReconciliationRecord`, matcher strategies, manual match, re-run | reconciliation DTOs, reconciliation events |
| Auditing | `AuditLog`, `Actor`, event subscriptions for every module | audit query DTOs |
| Reporting | Dashboard read model across schemas | dashboard DTOs |
| Support | `SupportTicket`, `SupportTicketMessage`, status workflow, tenant-scoped replies | support ticket DTOs |

Each module is a separate project with the same shape:

```
Modules/<Name>/
├── Contracts/      # DTOs, facade interfaces, integration events (public)
├── Domain/         # aggregates, value objects, domain events
├── Application/    # commands, queries, handlers, validators, facades
├── Infrastructure/ # EF configuration, repositories, external adapters
└── Endpoints/      # minimal API route mapping only
```

Reference graph: `Api → modules + Persistence` · `modules → SharedKernel + Persistence.Abstractions` · `Persistence → modules` (entity configurations only). Modules never reference another module's domain or EF types; cross-module writes go through `Contracts` facades or in-memory events, and cross-module reads are query projections.

### CQRS and Result

```
ICommand<T> / IQuery<T> → handler returns Result<T>
pipeline (outermost → innermost):
  ValidationBehavior → LoggingBehavior → ExceptionToResultBehavior → TransactionBehavior → handler
```

- The custom dispatcher (`SharedKernel/Cqrs/Dispatcher.cs`) resolves handlers from DI with a type cache — no MediatR dependency.
- `Result<T>` and `Error` live in `SharedKernel/Results`; expected failures never throw.
- Domain invariants may throw `DomainException`; one behavior converts it (and EF concurrency/unique violations) into a failed `Result`.
- Endpoints call `result.ToHttpResult()`, which produces `problem+json` with a stable `code` extension:

| Error type | HTTP |
|---|---|
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| Unprocessable | 422 |
| Unexpected | 500 |

### Transactions and events

`TransactionBehavior` opens one database transaction per command, runs the handler, drains domain events while the result is successful, saves once, then commits. A failing handler or event handler rolls the whole command back — the right default for money movement. There is no outbox and no broker; events are synchronous and in-process.

```
POST /api/webhooks/{provider}
  → HMAC verify + TryWrite(Channel)                    [endpoint, <5 ms]
  → 202 Accepted

WebhookIngressWorker (4 consumers)
  → claim insert (Received + payload_hash, unique provider/provider_event_id)
      duplicate hash → discard + duplicateCount++
      changed hash   → conflictCount++ + audit
  → ProcessWebhookEventCommand
      unknown type / invalid fields → Ignored / Failed (stored)
      payment.succeeded → PaymentRecorded
          → matcher by orderId → ReconciliationRecord + order allocation
          → audit handlers append AuditLog rows
      payment.failed → Payment Failed, no allocation
      provider payment conflict → Conflict (stored + audit)
  → Processed / Ignored / Failed / Conflict, one commit
```

Ack latency and persistence are intentionally decoupled; see the durability note above.

### Matching rules

Customer references are the external matching key: when supplied they must be unique (case-insensitive), and a duplicate create returns `409` with code `order.duplicate_reference`. Orders without a reference can coexist.

| Strategy | Condition | Outcome |
|---|---|---|
| OrderId | provider event carries the internal order id (webhook path) | matched / amount mismatch / review |
| ExactReference | payment reference equals an order number (manual path) | matched / amount mismatch / review |
| CustomerAndAmount | customer reference and amount both match | matched |
| AmountAndDate | no identifier supplied and the amount matches an order created within ±3 days | matched if a single candidate |
| None | no candidate | unmatched, still recorded |

`AmountAndDate` never runs when the payment carries an `orderId`, `reference` or `customerReference`, so a pre-order payment cannot be attached to an unrelated same-amount order. Amount and currency comparisons are exact. When a payment identifies an order but its amount differs from the order’s expected amount, the outcome is **AmountMismatch**: the payment is retained as `Unmatched`, the order is not modified, and the attempt is visible in the reconciliation mismatch filter. A currency mismatch produces an `Unmatched` record with a `Currency mismatch` reason. Manual match remains the explicit path to allocate a partial or overpayment.

More than one candidate at any step short-circuits to **Ambiguous** for manual review. Reconciliation is repeatable: re-running an `Unmatched` payment after its order exists matches it, while re-running a `Matched` or refunded payment returns `409`. Safety is enforced at the database level: a payment row is locked for the duration of a reconciliation/refund command, an order row is locked before allocation, and a partial unique index allows only one successful (`Matched` / `PartiallyMatched` / `ManuallyMatched`) reconciliation record per payment. A concurrent attempt rolls back instead of double-allocating.

Automatic matching never overpays an order: if allocating the payment would exceed the expected amount, the payment stays `Unmatched`, a reconciliation record with outcome `Overpaid` and a review reason is written, and the payment appears in the review queue. A currency mismatch is handled the same way with an explicit `Currency mismatch` reason, so the payment is still recorded instead of failing the request. `POST /api/reconciliation/manual-match` is the explicit operator path that can accept an overpayment.

Refunds are recorded manually (no provider refund API in this build). A refund can be partial or full, cannot exceed the payment's refundable amount, and writes a `Refund` row, an audit entry, and — when the payment was allocated — reverses the same amount from the order's paid total, recomputing its status. Refunding an unallocated/queued payment leaves the order untouched.

### Lifecycle state

The app's durable workflows are aggregate-owned; there is no shared state-machine framework. The important lifecycle rules are:

| Workflow | State and transition owner |
|---|---|
| Orders | `Order.Status` is derived from allocated payment totals; cancellation is terminal. See `Orders/Domain/Order.cs`. |
| Payments and refunds | `Payment` moves among received, matched/unmatched, failed, and partial/full refund states; `Refund` is a recorded outcome, not a workflow. See `Payments/Domain/Payment.cs` and `Refund.cs`. |
| Webhooks | Events move through received/processing to processed, ignored, failed, or conflict; failed events can be retried. Duplicate deliveries increment a counter without changing status. See `Webhooks/Domain/WebhookEvent.cs` and `Application/ProcessWebhookEvent.cs`. |
| Subscriptions | Status is combined with cancellation-at-period-end and pending-plan/period fields; transitions are coordinated by the aggregate and expiry worker. See `Tenancy/Domain/Subscription.cs` and `Infrastructure/SubscriptionExpiryWorker.cs`. |
| Invitations | Pending, accepted, revoked, and time-derived expiry; resend returns an expired invitation to pending. See `Tenancy/Domain/TenantInvitation.cs`. |
| Support tickets | Legal status edges are centralized in `SupportTicketWorkflow`; status timestamps are maintained by the aggregate. See `Support/Domain/SupportTicket.cs`. |
| Tenants, users, plans, refresh tokens | These use status/active/revocation fields rather than a complete transition graph. Tenant/user statuses are simple active/suspended flags; plans have an active flag; refresh-token activity is derived from expiry and revocation. |
| Reconciliation | Outcomes are immutable attempt records, not a mutable lifecycle. Matching and retry behavior belongs to the reconciliation/payment commands. |

In the client, TanStack Query owns request/mutation loading and error states; URL parameters own list filters and pagination. Local multi-step flows (sign-up, invitation acceptance, email change, and subscription confirmation) are component state. Model mutually exclusive actions as a discriminated union rather than separate booleans: payment match/refund dialogs follow this pattern on the dashboard and payment detail views, preventing impossible simultaneous-open states.

### Persistence

- Single `AppDbContext`, one PostgreSQL schema per module (`orders`, `payments`, `webhooks`, `reconciliation`, `audit`).
- Reconciliation safety: `FOR UPDATE` locks on the payment and order rows plus `ux_reconciliation_records_payment_success`, a partial unique index over successful outcomes per payment.
- snake_case tables, columns, keys, indexes and sequences via `EFCore.NamingConventions.UseSnakeCaseNamingConvention()`.
- `SnakeCaseModelValidator` hosted service fails startup in Development if any identifier drifts from snake_case.
- Provider payloads are stored as raw text (signature/hash fidelity); audit metadata is `jsonb`; enums are stored as strings; timestamps are `timestamptz`.
- Migration: `dotnet ef migrations add <Name> --project src/Persistence --startup-project src/Api`.

**Trade-off:** one DbContext across modules keeps transactions simple and is documented deliberately. Module isolation is enforced by project boundaries, `Contracts`-only public surfaces, and schema ownership. If the system grew real domain boundaries, split into per-module contexts with a shared connection/transaction.

### API surface

| Method | Route |
|---|---|
| POST | `/api/auth/register` · `/api/auth/login` · `/api/auth/refresh` · `/api/auth/logout` · `/api/auth/change-password` · `/api/auth/forgot-password` · `/api/auth/reset-password` |
| GET | `/api/auth/me` |
| POST | `/api/tenants/register` — optional `planCode`, defaults to Free |
| GET | `/api/tenants/mine` |
| GET/PUT | `/api/tenants/current` — name, business contact email, default currency, time zone; update is Owner/TenantAdmin |
| GET/POST | `/api/tenants/current/members` · PATCH/DELETE `/api/tenants/current/members/{userId}` |
| GET/POST | `/api/tenants/current/invitations` · POST `/api/tenants/current/invitations/{id}/resend` · `/revoke` |
| POST | `/api/invitations/accept` (anonymous or authenticated) |
| GET/PUT | `/api/tenants/current/subscription` — upgrade is immediate, downgrade is scheduled |
| POST | `/api/tenants/current/subscription/cancel` · `/renew` (idempotent) · `/reactivate` |
| GET | `/api/tenants/current/subscription/history` |
| GET | `/api/tenants/current/entitlements` · `/api/usage` · `/api/usage/features/{featureKey}` |
| GET/PATCH | `/api/account/profile` · POST `/api/account/change-email` · `/change-email/confirm` |
| POST/GET | `/api/support/tickets` · GET `/api/support/tickets/{id}` · POST `/{id}/messages` · PATCH `/{id}/status` |
| GET | `/api/plans` (anonymous, database-backed) |
| GET | `/api/platform/tenants` (platform admin, no tenant context) |
| GET | `/api/dashboard/summary` |
| GET/POST | `/api/orders` (JWT) · supports `status`, `search`, `sort` |
| GET | `/api/orders/{id}` |
| POST | `/api/orders/{id}/cancel` |
| GET/POST | `/api/payments` |
| GET | `/api/payments/{id}` |
| POST | `/api/webhooks/{provider}` (anonymous, HMAC + tenant subdomain) |
| GET | `/api/webhooks` · `/api/webhooks/{id}` · supports `paymentId` |
| GET | `/api/reconciliation` · `/api/reconciliation/payment/{paymentId}` · supports `outcome`, `search`, `reason`, `from`, `to` |
| POST | `/api/reconciliation/manual-match` |
| POST | `/api/payments/{id}/link` · `/api/payments/{id}/reconcile` |
| POST/GET | `/api/payments/{id}/refunds` |
| POST | `/api/webhooks/{id}/reprocess` — retry a plan-limit-failed event |
| GET | `/api/audit-logs` |
| GET | `/health` |

Paging is `page`/`pageSize` (max 100) returning `items`, `page`, `pageSize`, `totalCount`, `totalPages`.

Dashboard flow counters are **all-time**: orders, webhooks, recorded payments, successful payments (everything except `Failed`), currently `Matched` payments, currently `Unmatched`/pending-review payments, and manual matches. `matchedRate7d` is calculated from reconciliation outcomes in the last seven days.

## Client

```
web/src/
├── app/            router (lazy routes), providers
├── components/ui/  shadcn primitives
├── components/layout/  app shell, sidebar, top bar, page header
├── components/data/    data table, status badge, stat card, sparkline, dialogs, detail list…
├── features/       dashboard, orders, payments, webhooks, reconciliation, audit
├── public-site/    public layout, navigation, marketing pages, auth pages
├── lib/            api client, session, formatting, status mapping, URL table state
└── types/          DTO types mirroring the API
```

- **Design system:** shadcn/ui New York with an indigo primary, 12px radius, Inter Variable, a soft neutral canvas and white bordered cards. Tokens live in `src/index.css` (`@theme inline` exposes `success`/`warning`/`sidebar` roles in addition to the shadcn defaults).
- **Composition:** the dashboard is exception-first — five operational KPIs, the review queue, matched-coverage health, and live recent payment/reconciliation feeds. Reconciliation is a workspace with outcome/date/reason filters and a failed-payments section. Payments open on a full detail page with copyable identifiers, amount/currency comparison, reconciliation history, linked webhook events and an audit timeline.
- **State:** TanStack Query with query keys per feature; lists are URL-driven (filters, sorting and pagination survive reloads and are shareable).
- **Errors:** the API client turns `problem+json` into a typed `ApiError`; screens show inline error states with retry and toasts for mutations.
- **Commands:** filters, dialogs and tables are keyboard reachable; row actions do not hijack keyboard events from nested links/buttons; status is never conveyed by color alone.

### Public site

`web/src/public-site/` is a separate module from the product app, with its own layout, navigation and footer.

| Route | Page |
|---|---|
| `/` | Home — hero, problems, capabilities, workflow preview, pricing preview |
| `/product` · `/how-it-works` · `/pricing` · `/about` · `/demo` | Queued for stage 2; placeholders today |
| `/sign-in` · `/sign-up` | Real auth, wired to Identity and Tenancy |

- Public routes are anonymous; `/dashboard`, `/orders`, `/payments`, `/webhooks`, `/reconciliation` and `/audit` keep their paths and require a session.
- The product dashboard moved from `/` to `/dashboard`.
- The header uses the shared design system primitives (dropdown, sheet, buttons) and adapts to mobile with a slide-over menu.
- `GET /api/plans` is anonymous and is the only source of plan names, features and limits; nothing is hardcoded in the client.
- Auth is stored client-side; the API client attaches `Authorization` and `X-Tenant-Slug`, and refreshes once on `401`.
- `ReconciliationDiagram` animates with CSS only and is disabled under `prefers-reduced-motion`.

### Scripts

```bash
npm run dev      # Vite dev server, /api proxied to :5080
npm run build    # tsc -b && vite build
npm run lint     # oxlint

# webhook simulator (repo root)
node scripts/simulate-webhooks.mjs --scenario all
```

## Deliberate omissions

- **Webhook ingress is a CLI simulator, not a provider UI.** `scripts/simulate-webhooks.mjs` covers the scenarios; there is no browser payload generator.
- **In-memory ingress queue.** The <5 ms ack is achieved by acknowledging before persistence. A crash can lose queued events; there is no outbox/broker. Stored events stuck in `Received/Processing` are failed by a recovery sweep. Only plan-limit failures can be reprocessed (`POST /api/webhooks/{id}/reprocess`); there is no general replay endpoint.
- **No real payment provider.** Plans use manual/simulated activation; no invoices, gateway calls or card data are involved. Email delivery is a logging sender; a real provider can replace `IEmailSender`.
- **No Kafka/RabbitMQ, no outbox.** In-process events inside the command transaction are sufficient at this scale and keep the failure mode obvious.
- **No refunds via a payment provider.** Refunds are recorded manually as succeeded (no Stripe refund call, no refund webhooks), and there are no refund approval roles. Partial manual splits, FX conversion and dark mode are also omitted (tokens are in place for dark mode).
- **No click-to-sort on tables yet.** Lists support server-side filtering and pagination; sorting is a follow-up API/UI addition.
- **Order detail is composed client-side** from the order, payments and reconciliation endpoints (three parallel queries) rather than one aggregate read endpoint.
- **Customer-reference uniqueness is enforced in the create-order handler plus a lookup index**, not a database unique constraint, because pre-existing dev rows can contain duplicates. After cleaning those rows, the index can be made unique.

## Manual acceptance checklist

1. Dashboard numbers match the database; the flow counts and chart render.
2. A signed webhook returns `202` with `X-Webhook-Ack-Ms` under 5 ms, and the payment/order update asynchronously.
3. Replaying the same event id with the same payload returns `202`, increments `duplicateCount`, and creates no second payment; a mutated replay increments `conflictCount`.
4. Unknown event types are `Ignored`; events with no identifier (orderId/reference/customerReference) and invalid payloads are `Failed`; bad signatures return `401` and store nothing.
5. `payment.failed` records a `Failed` payment without allocating to the order; `node scripts/simulate-webhooks.mjs --scenario all` passes all 13 scenarios.
6. `--scenario e2e` passes E2E-01..E2E-06: normal payment, four duplicate deliveries, amount mismatch, unknown order, failure/retry, and late-arriving order.
7. A payment arriving before its order stays `Unmatched` and visible; creating the order and re-running reconciliation matches it.
8. A $90 payment against a $100 order produces `AmountMismatch`, leaves the order `Pending`, and appears in the mismatch filter and review queue.
9. Ten simultaneous re-runs of one unmatched payment produce exactly one successful reconciliation record and allocate the amount once (`409` for the rest).
10. An unmatched payment appears in the review queue; manual match updates the order, payment and audit log.
11. Order detail shows multiple payment attempts and the reconciliation trail; cancel is blocked once money is allocated.
12. Refund a matched payment partially, then fully: the payment shows `PartiallyRefunded`/`Refunded`, the order's paid amount reverses and its status recomputes, and audit entries `refund.recorded` / `order.payment_reversed` appear. An over-refund returns `409`.
13. Refund a queued overpayment: the payment leaves the review queue as `Refunded` and the order is untouched.
14. Filters and pagination survive a reload; empty, loading and error states are reachable.
15. Keyboard-only pass through navigation, filters, manual match/refund and confirmation dialogs.
