# Frontend browser review

## Test matrix

- Desktop: `1440 × 900`, DPR 2.
- Tablet: `768 × 1024`.
- Mobile: `390 × 844`, DPR 3.
- Touch emulation: CDP `Emulation.setTouchEmulationEnabled`, 5 touch points.
- Network: CDP `Network.emulateNetworkConditions` — 150 ms latency, 1.6 Mbps download, 750 kbps upload, cache disabled, `cellular3g`.
- Browser automation: `agent-browser` 0.42.0, Chrome 157.

All pages were checked at 390 px after the fixes. `document.documentElement.scrollWidth` equals `innerWidth` on:

`/`, `/orders`, `/payments`, `/reconciliation`, `/webhooks`, `/audit`, `/payments/:id`

Tablet and desktop also report no page-level horizontal overflow.

## Fixes

### 1. Mobile dashboard overflow and overlapping review row

The Recent Payments/Reconciliation cards were 514 px wide inside a 390 px viewport because grid children retained min-content width. The review-queue provider link was inline, so its long ID overlapped the amount and actions.

- Before: `before/mobile-dashboard.png` — page scroll width 530 px.
- After: `after/mobile-dashboard.png` — page scroll width 390 px.
- Changed: added `min-w-0` to dashboard cards/rows, made the queue provider link block/truncated, and narrowed the queue action area.

### 2. Mobile data tables hid the important columns

Orders, Payments and Reconciliation put status/amount/result off-screen behind horizontal scrolling.

- Before: `before/mobile-orders.png`, `before/mobile-payments.png`, `before/mobile-reconciliation.png`.
- After: `after/mobile-orders.png`, `after/mobile-payments.png`, `after/mobile-reconciliation.png`.
- Changed: responsive column priorities and compact mobile cells. Orders now show order reference + amount + status; payments show provider, amount and status; reconciliation shows payment + actual + result. Secondary fields remain available at larger widths.

### 3. Payment detail overflow on mobile

A long provider payment ID expanded the Transaction card to 438 px, and the three header actions did not shrink inside 390 px.

- Before: `before/mobile-payment-detail.png` — page scroll width 494 px.
- After: `after/mobile-payment-detail.png` — page scroll width 390 px.
- Changed: constrained the detail grid, list rows, and copyable IDs; header actions now wrap to a second row on narrow screens.

### 4. Blank screen on throttled 3G

The initial HTML only contained an empty `#root`; on 3G the dev bundle took long enough that the page was completely blank. The app now paints a branded loading state before JavaScript is ready.

- Before: `before/3g-mobile-dashboard.png` — blank white page.
- After: `after/3g-mobile-boot.png` — immediate boot state.
- After load: `after/3g-mobile-dashboard-loaded.png` — dashboard while recent feeds are still loading their skeletons.
- Changed: inline boot loader in `web/index.html`, removed by `main.tsx` once React mounts.

Note: this is a Vite **development** server measurement. It serves many unbundled modules; the boot state makes the slow hydration visible, but this is not a production-bundle timing claim.

### 5. Desktop review queue height and misleading currency

The review queue stretched to the health card height with a large blank area, and the unmatched aggregate displayed `$150.00` even though the queued payment was `£150.00`.

- Before: `before/desktop-dashboard.png`.
- After: `after/desktop-dashboard.png`.
- Changed: queue card keeps a footer with “Open payments” and “Oldest exceptions shown first”; unmatched value is labelled `Unmatched value (raw)` and no longer invents a currency symbol.

### 6. Tablet verification

- Before: `before/tablet-dashboard.png`.
- After: `after/tablet-dashboard.png` — 768 px, no page overflow; two-column KPI layout and full-width queue/health cards.

## Interaction checks with touch emulation

- Mobile navigation drawer: `after/mobile-navigation.png`.
- Create order dialog: `after/mobile-create-order-dialog.png`.
- Record payment dialog: `after/mobile-record-payment-dialog.png`.

The first record-payment screenshot caught the dialog mid-animation; a recapture after the animation is solid. Both dialogs fit at 390 px and report no page overflow.

## Console

No browser console errors were observed during the final page audit. The only console output was Vite HMR/debug messages and the React DevTools info message.
