# Web architecture

Ledgerline's web app is a React 19 SPA (Vite) talking to the .NET API. This document is
the contract a new contributor must follow. It describes what exists today, not an
aspiration.

---

## 1. State: four layers, no overlap

| Layer | Owner | Examples | Lifetime |
|---|---|---|---|
| Server state | TanStack Query | payments, orders, dashboard, members | per tenant, until invalidated |
| Session state | `localStorage` + `useSyncExternalStore` (`lib/session.ts`) | user, tenant, role, tokens | until sign-out |
| URL state | `useTableParams` (`lib/use-table-params.ts`) | page, pageSize, filters, sort, search | shareable, survives reload |
| Local UI state | `useState` | dialog open, hover, form draft | component mount |

Rules:

- **Server data never goes into `useState`.** Read it from `useQuery`.
- **Filters never go into `useState`.** They belong in the URL so a filtered view is shareable and the back button works.
- **Local UI state must not be reset by an effect.** Put the stateful part inside the component that unmounts (e.g. inside `DialogContent`), as `refund-dialog` and `manual-match-dialog` now do.
- **Session state is written in exactly one place per concern.** `useTenantSessionSync` owns tenant identity; nothing else calls `updateSessionTenant` except sign-in and tenant switching.

---

## 2. Query keys — always tenant-scoped

Every key is built by `lib/query-keys.ts`. Never write a literal key.

```ts
// good
useQuery({ queryKey: qk.payments(tenantId, { page, status }), queryFn: ... })

// never
useQuery({ queryKey: ['payments', { page, status }], queryFn: ... })
```

Every key is `[root, tenantId, ...rest]`. The tenant id is what prevents one workspace's
rows rendering under another. `RequireSession` in `app/router.tsx` also clears the cache
when the tenant changes, which bounds memory and closes the window where a stale entry
could be read.

**Invalidation uses the root key**, e.g. `queryClient.invalidateQueries({ queryKey: qk.paymentsRoot })`,
so it matches every tenant scope.

`tenantId` comes from `useTenantKey()`. It is `null` for platform-admin and public views,
which read platform-level data rather than workspace data.

### Time and platform-scoped keys

Queries whose data is not tenant-owned stay literal: public plan catalogue (`['plans']`),
platform-admin views (`['platform', …]`). These are deliberately outside the tenant scope.

---

## 3. Invalidation — declare it once

`lib/invalidate.ts` maps a resource to the roots it affects:

```ts
invalidateRelated(queryClient, 'payment')
// → payments, reconciliation, dashboard, orders, refunds, audit
```

Add a new resource there rather than hand-listing keys at the call site. Hand-written
lists drift: `record-payment-dialog` used to invalidate four keys while
`payment-detail-page` listed a fifth.

---

## 4. Errors — one path

All query failures render through `errorStateProps` (`lib/api-client.ts`):

```tsx
{query.isError ? <ErrorState {...errorStateProps(query.error)} onRetry={() => void query.refetch()} /> : ...}
```

`errorMessage()` handles `ApiError`, `TypeError` (API unreachable) and unknown values.
Raw `error.message` leaks transport text such as "Failed to fetch" and drops the API error
code, so the same failure reads differently per screen. Don't reintroduce it.

Mutations report via `toast.error(errorMessage(error))`.

---

## 5. Data fetching shape

Every list page follows the same five states:

1. `isError` → `ErrorState` with retry
2. `isLoading` → table skeleton (`DataTable isLoading`)
3. empty → `EmptyState` (with a distinct message when filters are active)
4. data → `DataTable`
5. `ListPagination`

Loading regions carry `aria-busy` so screen readers don't announce half-rendered content.

### staleTime

The default is 30 s (`app/providers.tsx`). Override per query where the data's refresh
behaviour differs:

- dashboard summary — `0` (live operational view)
- public plan catalogue — `5 min`
- tenant settings — `60 s`

---

## 6. Component tiers

| Tier | Location | Rules |
|---|---|---|
| Primitives | `components/ui/` | shadcn-generated. Do not hand-edit beyond what `shadcn add` would produce; changes are overwritten. |
| Product components | `components/data/`, `components/layout/` | Own the product vocabulary. Safe to extend. |
| Feature components | `features/<feature>/` | Page plus its dialogs. May use the tiers above, never the reverse. |
| Public site | `public-site/` | Marketing/auth pages. Shares primitives, not feature components. |

Dependency direction is one-way: `ui` ← `data` ← `features`.

---

## 7. Routing and code splitting

`app/router.tsx` lazy-loads all 24 feature pages via `lazy()` + `withSuspense`. New pages
must be lazy too; eager imports pull a feature into the initial bundle.

`AppShell` owns the authenticated frame: sidebar, top bar, skip link, and route-change
focus. Page content renders into `<main id="main">`.

---

## 8. Accessibility and motion conventions

- **Pending actions**: `<Button loading={mutation.isPending}>` — sets `disabled` + `aria-busy` and shows a spinner in a fixed-width slot so the label does not shift. Never build this by hand.
- **Motion**: 150 ms for controls, 200 ms for overlays, transform/opacity only. Every animation carries `motion-reduce:animate-none`. No new motion library — `tw-animate-css` primitives are the vocabulary.
- **Tables**: `TableHead` sets `scope="col"`. Sortable columns set `aria-sort` and wrap the header in a button; pass `sortKey`, `sortDirection` and `onSortChange` to `DataTable`.
- **Filters**: `FilterBar` is a `role="group"` labelled "Filters".
- **Focus**: visible `:focus-visible` ring everywhere; Radix returns focus to the trigger on close.
- **Dialogs**: always include `DialogTitle` and `DialogDescription`.

---

## 9. Conventions checklist

- [ ] Query key from `qk.*`, tenant-scoped
- [ ] Invalidation via `invalidateRelated`
- [ ] Errors via `errorStateProps`
- [ ] All five list states handled
- [ ] Filters in the URL via `useTableParams`
- [ ] Pending buttons use `loading`
- [ ] No `setState` in an effect to reset a form
- [ ] New pages lazy-loaded
