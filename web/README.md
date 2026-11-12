# Ledgerline web client

React 19 + TypeScript + Vite + Tailwind CSS v4 + shadcn/ui.

```bash
npm install
npm run dev      # http://localhost:5173, /api proxied to http://localhost:5080
npm run build    # tsc -b && vite build
npm run lint     # oxlint
```

The API must be running for data to load. See the root [README](../README.md) for architecture, the design system and seeded demo data.

## Layout

- `src/app` — router (lazy routes) and providers.
- `src/components/ui` — shadcn primitives (New York, slate, CSS variables).
- `src/components/layout` — app shell, sidebar, top bar, page header.
- `src/components/data` — table, status badge, money, stat card, sparkline, dialogs, detail list, timeline.
- `src/features/*` — one folder per module: `api.ts` + pages + feature components.
- `src/lib` — API client, formatting, status mapping, URL table state.
- `src/types/api.ts` — DTOs mirroring the API contracts.

Design tokens live in `src/index.css`; `@` is aliased to `src`.
