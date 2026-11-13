import { Link, Outlet } from 'react-router-dom'
import { ArrowLeft, Check } from 'lucide-react'
import { BrandMark } from '@/components/brand/brand-mark'

const proofPoints = [
  'Match orders to payments automatically',
  'Work every exception from one review queue',
  'Keep an auditable trail of each decision',
]

export function AuthLayout() {
  return (
    <div className="min-h-dvh bg-card lg:grid lg:grid-cols-[minmax(0,1fr)_minmax(0,560px)]">
      <aside className="relative hidden flex-col justify-between overflow-hidden border-r border-border/70 bg-background px-10 py-10 lg:flex">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 bg-[radial-gradient(80%_60%_at_20%_0%,var(--color-primary)/10%,transparent)]"
        />

        <div className="relative">
          <BrandMark />
        </div>

        <div className="relative max-w-md">
          <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-primary">Payment operations</p>
          <h2 className="mt-4 text-3xl font-semibold leading-tight tracking-tight text-foreground">
            Close the gap between orders and payments.
          </h2>
          <p className="mt-4 text-sm leading-6 text-muted-foreground">
            Ledgerline reconciles provider payments against your orders, surfaces exceptions, and keeps every
            decision auditable.
          </p>

          <ul className="mt-8 space-y-3">
            {proofPoints.map((point) => (
              <li key={point} className="flex items-start gap-2.5 text-sm text-muted-foreground">
                <span className="mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full bg-success/10 text-success">
                  <Check className="size-3" aria-hidden />
                </span>
                {point}
              </li>
            ))}
          </ul>
        </div>

        <p className="relative text-xs text-muted-foreground">Built for finance and operations teams.</p>
      </aside>

      <div className="flex min-h-dvh flex-col bg-card">
        <header className="flex h-16 items-center justify-between px-4 sm:px-6 lg:justify-end">
          <Link
            to="/"
            aria-label="Ledgerline home"
            className="rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60 lg:hidden"
          >
            <BrandMark subtitle={false} />
          </Link>
          <Link
            to="/"
            className="inline-flex items-center gap-1.5 rounded-md text-sm font-medium text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
          >
            <ArrowLeft className="size-4" aria-hidden />
            Back to site
          </Link>
        </header>

        <main className="flex flex-1 justify-center px-4 py-8 sm:px-6 lg:px-12">
          <div className="my-auto w-full max-w-md">
            <Outlet />
          </div>
        </main>

        <footer className="px-4 pb-6 text-center text-xs text-muted-foreground sm:px-6">
          Ledgerline · Payment reconciliation
        </footer>
      </div>
    </div>
  )
}
