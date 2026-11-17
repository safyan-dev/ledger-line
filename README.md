# Ledgerline — Payment Reconciliation

Ledgerline is a multi-tenant payment reconciliation system designed for business and finance teams.

It helps businesses track orders, record payments from external providers, and reconcile transactions automatically. Payments that cannot be matched confidently are sent for manual review.

The project focuses on reliable payment processing, data consistency, tenant isolation, and clear operational visibility.

## How It Works

```text
Provider Webhook
       |
       v
  Webhook Event
       |
       v
     Payment
       |
       v
  Reconciliation
       |
       +------> Order Allocation
       |
       +------> Audit Log
```

## Core Features

- **Order Management** — Create and track orders, expected amounts, and payment status.
- **Payment Processing** — Record successful and failed payments from simulated providers.
- **Webhook Handling** — Verify signatures, handle duplicate events, and process events asynchronously.
- **Payment Reconciliation** — Match payments using order IDs, references, customer details, and amounts.
- **Manual Review** — Handle unmatched, ambiguous, overpaid, and mismatched transactions.
- **Multi-tenancy** — Isolate tenant data and enforce membership-based access.
- **Team Management** — Manage members, invitations, and tenant roles.
- **Subscriptions and Usage** — Manage plans, features, and usage limits.
- **Audit Logging** — Keep a record of important business operations.
- **Observability** — Monitor API health, payment activity, webhook processing, and reconciliation.

> **Note:** This project uses simulated payment providers. It does not process real payments or move money.

## Tech Stack

| Area | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs, C# |
| Architecture | Modular Monolith, CQRS, Domain-Driven Design |
| Database | PostgreSQL 17, EF Core 10 |
| Frontend | React 19, TypeScript, Vite |
| UI | Tailwind CSS v4, shadcn/ui |
| Client State | TanStack Query, React Router 7 |
| Testing | xUnit, Testcontainers |
| Performance | k6 |
| Observability | OpenTelemetry, Prometheus, Grafana, Jaeger |
| Infrastructure | Docker Compose |

## Architecture

The backend follows a modular monolith architecture. Each business module owns its domain logic, application handlers, infrastructure, and API endpoints.

Main modules:

- **Identity & Tenancy** — Authentication, memberships, and tenant resolution.
- **Orders** — Order lifecycle and payment allocation.
- **Payments** — Payment records and provider idempotency.
- **Webhooks** — Signature verification, event ingestion, and background processing.
- **Reconciliation** — Matching strategies, manual matching, and reconciliation history.
- **Auditing** — Business activity tracking.
- **Reporting** — Operational dashboard data.
- **Support** — Tenant-scoped support tickets.

The application uses a custom CQRS dispatcher, a Result pattern for application errors, and domain events for communication between modules.

PostgreSQL transactions, row-level locks, unique constraints, and tenant-aware relationships help protect data consistency and prevent duplicate payment allocations.

## Webhook Processing

Webhook endpoints verify provider signatures and quickly acknowledge accepted events. A background worker handles persistence and payment processing.

The implementation covers:

- Duplicate webhook delivery.
- Invalid signatures and payloads.
- Failed processing and retries.
- Payments received before their orders.
- Amount and currency mismatches.
- Concurrent webhook deliveries.

**Current limitation:** Webhook ingress uses a bounded in-memory queue before database persistence. A process crash can therefore lose an acknowledged event. A durable queue or outbox is a planned improvement.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- Docker

### 1. Start infrastructure

```bash
docker compose up -d
```

This starts PostgreSQL, pgAdmin, and the observability services.

### 2. Configure local secrets

Local secrets are stored with .NET user-secrets and are never committed. Run the setup script for your shell:

```bash
./backend/tools/setup-dev-secrets.sh
```

```powershell
./backend/tools/setup-dev-secrets.ps1
```

The script keeps existing values and fills in missing ones. It configures:

| Key | Purpose |
|---|---|
| `ConnectionStrings:Database` | Local PostgreSQL connection |
| `Jwt:SigningKey` | Access-token signing key |
| `Webhooks:SigningSecret` | Fallback webhook signing secret |
| `Webhooks:Providers:mock:SigningSecret` | Mock provider secret |
| `Webhooks:Providers:stripe:SigningSecret` | Stripe-shaped provider secret |
| `Demo:Password` | Local demo owner password |
| `Demo:PlatformPassword` | Local platform owner password |

Override any generated value with `DB_CONNECTION`, `JWT_SIGNING_KEY`, `WEBHOOK_SIGNING_SECRET`, `WEBHOOK_MOCK_SECRET`, `WEBHOOK_STRIPE_SECRET`, `DEMO_PASSWORD`, or `PLATFORM_PASSWORD`.

The script prints the local demo credentials. Defaults are:

| Account | Credentials |
|---|---|
| Demo owner | `avery.chen@ledgerline.local` / `Demo123!ChangeMe` |
| Platform owner | `platform@ledgerline.local` / `Platform123!ChangeMe` |
| Demo tenant | `demo` subdomain |

These are local-only values. Deployed environments must use environment variables or a secret store:

```text
ConnectionStrings__Database
Jwt__SigningKey
Webhooks__SigningSecret
Webhooks__Providers__mock__SigningSecret
Webhooks__Providers__stripe__SigningSecret
Demo__Password
Demo__PlatformPassword
Web__PublicBaseUrl
```

`appsettings.json` contains no secrets. User-secrets are plaintext local storage and are not suitable for production.

For design-time EF commands such as `dotnet ef`, export `ConnectionStrings__Database` first; runtime configuration uses user-secrets in Development.

### 3. Run the backend

```bash
cd backend

dotnet build
dotnet run --project src/Api --launch-profile http
```

API: `http://localhost:5080`

API documentation: `http://localhost:5080/scalar`

### 4. Run the frontend

```bash
cd web

npm install
npm run dev
```

Frontend: `http://localhost:5173`

The API applies migrations on startup. Development seeds demo data only when `Database:SeedDemoData` is `true`, which the Development settings enable explicitly.

## Testing

Run the backend test suite:

```bash
cd backend

dotnet test tests/PaymentReconciliation.Tests/PaymentReconciliation.Tests.csproj
```

The tests use PostgreSQL through Testcontainers and cover business workflows, authorization, tenant isolation, subscription limits, and concurrent operations.

### Webhook Simulation

The simulator needs the local mock-provider secret from user-secrets:

```bash
export WEBHOOK_SECRET="$(dotnet user-secrets list --project backend/src/Api | sed -n 's/^Webhooks:Providers:mock:SigningSecret = //p')"
```

Run the webhook scenarios:

```bash
node scripts/simulate-webhooks.mjs --scenario e2e
```

Run all scenarios:

```bash
node scripts/simulate-webhooks.mjs --scenario all
```

Test concurrent webhook delivery:

```bash
node scripts/simulate-webhooks.mjs --scenario concurrent --iterations 20
```

## Observability

The project includes an OpenTelemetry-based observability setup.

| Service | Local URL |
|---|---|
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Jaeger | http://localhost:16686 |
| pgAdmin | http://localhost:5050 |

It tracks API performance, application health, payment activity, webhook processing, reconciliation outcomes, and database metrics.

## Project Structure

```text
/
├── backend/
│   ├── src/
│   │   ├── Api/
│   │   ├── Modules/
│   │   ├── Persistence/
│   │   └── SharedKernel/
│   └── tests/
│
├── web/
│   └── src/
│       ├── app/
│       ├── features/
│       ├── components/
│       ├── public-site/
│       └── lib/
│
├── docker/
├── perf/
└── scripts/
```

## Engineering Focus

The main engineering goals behind Ledgerline are:

- Building maintainable business modules with clear boundaries.
- Handling asynchronous payment events safely.
- Preventing duplicate processing and inconsistent financial records.
- Enforcing tenant isolation at both application and database levels.
- Designing concurrency-safe operations.
- Making business workflows observable and testable.
- Enforcing authentication lockout, rate limits, refresh-token rotation with reuse detection, and immediate access-token invalidation on logout or credential changes.
- Keeping inactive subscriptions read-only while preserving billing and reactivation access.
- Requiring email confirmation for self-service signups and recording platform-scoped audit events for administrative changes.

This project is an ongoing implementation, with a focus on practical engineering decisions and the trade-offs involved in building a SaaS application.