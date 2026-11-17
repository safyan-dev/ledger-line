# Engineering Standards — api (backend)

## Mandatory Agent Instructions

- Coding agents must read and follow this file before generating or changing code in this repository.
- Apply these standards to implementation, tests, and generated code. Do not bypass them for speed.
- For ASP.NET Core / .NET web framework guidance, also follow the `aspnet-core` skill (prompt trigger: `aspnet core`).
- Do not create or update markdown/docs files unless the user explicitly asks, or a contract/behavior change requires updating existing docs under `docs/`.

## Engineering Philosophy

- Prioritize correctness, data integrity, and tenant/auth boundaries over speed.
- Prefer simple, maintainable solutions that follow existing patterns and the current directory structure in this repo. Place new code where similar code already lives; do not invent parallel folders or layers.
- Avoid unnecessary abstractions and duplicated logic.
- Treat quick fixes as harmful. Never patch over an architectural problem, paper over a failed design, or ship a workaround that leaves the wrong structure in place. Fix the root cause with the complete, maintainable solution that matches existing architecture.
- Explain important engineering trade-offs, especially around transactions, persistence, and reliability.

## Before Implementation

- Read the affected code, tests, and relevant docs under `docs/` before making changes.
- Identify affected layers, cross-layer contracts, dependencies, and API/client impacts.
- Make a concise implementation plan for non-trivial work; identify security and reliability risks.
- Preserve layer boundaries: controllers stay thin; business rules live in Application; persistence in Persistence; external providers in Infrastructure.

## Architecture and Code Quality

- Keep the layered monolith: Presentation (`Seeu-api`), Application (`Seeu-Application`), Domain/Core (`Seeu-Core`), Persistence (`Seeu-Persistence`), Infrastructure (`Seeu-Infrastructure`).
- Prefer Application services and existing abstractions over inventing a new dispatcher unless the design already uses one.
- Business-rule and expected domain failures must not throw. Return `Result<T>` (or the existing equivalent) with clear, actionable error messages a client or operator can understand. Map those to HTTP/`ProblemDetails` at the API boundary (see `docs/decisions/`).
- Throw only for truly exceptional/unexpected conditions (bugs, invariant corruption, infrastructure failures that cannot be expressed as a domain outcome). Never use exceptions as control flow for validation, authorization denials, not-found, conflict, or other business outcomes.
- Error messages must help the caller fix or understand the outcome without exposing internal system state, implementation details, raw exception text, or stack traces to API clients.
- Keep controllers focused on HTTP concerns; keep business rules in Application/Domain.
- Use explicit validation (FluentValidation where present) and error handling. Do not silently swallow failures or return success-shaped fallbacks.
- Keep database writes transactionally consistent via `IUnitOfWork.ExecuteInTransactionAsync` (or the existing equivalent). Do not leave partial writes across multi-step mutations.
- Scope reads/writes to the authenticated user/tenant context resolved on the server. Do not trust client-supplied ownership IDs without authorization checks.
- Do not add comments to source code. Express intent through clear names and structure; put necessary design explanations in `docs/` only when documentation is required.
- When changing a workflow, inspect its implicit state machine: status enums, booleans, nullable timestamps, retries, and related fields can jointly define states and transitions.
- Enumerate valid states, allowed transitions, terminal states, invalid transitions, and failure/retry paths. Ensure every state is handled correctly and consistently across commands, queries, persistence, jobs, and UI contracts. Missing or inconsistent handling is a defect.
- Add or update tests for relevant transitions, invalid or terminal states, retries, and concurrency. Do not introduce a state-machine abstraction unless the existing design or requirements call for one.
- Hangfire job types own their `ExecuteAsync` bodies under Application `Jobs/`. Services schedule via the existing background-job abstraction; do not leave Hangfire entry methods as one-line forwarders on services.

## Security

- Never add real credentials, signing keys, tokens, or other secrets to source control. `appsettings.Development.json` is development-specific; base `appsettings.json` is loaded in other environments too and must not contain production credentials or secrets.
- Load production secrets from deployment configuration or a secret store.
- Apply least privilege and enforce authentication and role/permission checks at API resource boundaries, not only in the client.
- Validate external input, including API requests, webhook signatures, and auth context.
- Do not trust tenant/user/event IDs supplied by clients as authorization; use server-resolved identity and existing authorize filters/handlers.
- Avoid logging passwords, tokens, secrets, raw request bodies, or customer-sensitive data.
- Review security and maintenance implications before adding or upgrading dependencies.

## Testing and Verification

- After completing the task, always run verification: formal checks (relevant unit/integration tests) and runtime checks when feasible (build, targeted manual/API exercise of the changed path).
- Add or update focused tests alongside behavior changes. Cover relevant edge cases, authorization failures, isolation, and concurrency.
- Never remove or weaken tests merely to make a build pass.
- Run the smallest relevant test set, then expand validation when the change warrants it.
- Backend test suite:

  ```bash
  cd uns-api
  dotnet test tests/Seeu-tests/Seeu-tests.csproj
  ```

- Report commands run and their outcomes. Clearly identify checks that were not run.

## Observability

- Use existing structured logging and OpenTelemetry instrumentation patterns in the repo.
- Include useful operational context without logging sensitive data.
- Instrument important business and infrastructure operations with appropriate metrics and traces when extending hot paths.
- Surface failures explicitly; do not hide errors behind broad catches or empty results.

## Agent Workflow

1. Understand the requirement and existing implementation.
2. Plan the change and identify risks.
3. Implement the smallest complete change in the existing structure.
4. Run formal and runtime verification for the change.
5. Review the diff for regressions, unrelated changes, unsolicited docs, and comments.
6. Report the result and verification status.

## Definition of Done

- The implementation meets the request and follows existing architecture and directory structure.
- Formal and runtime verification was run (or any limitation is reported).
- Security and auth/isolation implications have been considered.
- Business failures return helpful `Result`/ProblemDetails errors; no exception-based business control flow; no internal state leaked to clients.
- Implicit state machines touched by the change are fully handled.
- No unintended changes, secrets, source comments, or unsolicited markdown files are introduced.
- Related documentation under `docs/` is updated only when behavior or contracts change and that update is warranted.
