import { Link } from 'react-router-dom'
import { BrandMark } from '@/components/brand/brand-mark'

export function PublicFooter() {
  return (
    <footer className="border-t border-border/80 bg-card">
      <div className="mx-auto grid w-full max-w-6xl gap-10 px-4 py-12 sm:px-6 md:grid-cols-[1.4fr_1fr_1fr]">
        <div className="space-y-4">
          <BrandMark />
          <p className="max-w-sm text-sm text-muted-foreground">
            A payment reconciliation workspace for teams that need to know which orders are actually paid,
            which payments are still waiting, and why.
          </p>
        </div>

        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">Product</p>
          <ul className="mt-3 space-y-2 text-sm">
            <li>
              <Link to="/product" className="text-muted-foreground transition-colors hover:text-foreground">
                Product overview
              </Link>
            </li>
            <li>
              <Link to="/how-it-works" className="text-muted-foreground transition-colors hover:text-foreground">
                How it works
              </Link>
            </li>
            <li>
              <Link to="/pricing" className="text-muted-foreground transition-colors hover:text-foreground">
                Pricing
              </Link>
            </li>
            <li>
              <Link to="/demo" className="text-muted-foreground transition-colors hover:text-foreground">
                Interactive demo
              </Link>
            </li>
          </ul>
        </div>

        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">Account</p>
          <ul className="mt-3 space-y-2 text-sm">
            <li>
              <Link to="/about" className="text-muted-foreground transition-colors hover:text-foreground">
                About
              </Link>
            </li>
            <li>
              <Link to="/sign-in" className="text-muted-foreground transition-colors hover:text-foreground">
                Sign in
              </Link>
            </li>
            <li>
              <Link to="/sign-up" className="text-muted-foreground transition-colors hover:text-foreground">
                Get started
              </Link>
            </li>
          </ul>
        </div>
      </div>

      <div className="border-t border-border/80">
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-2 px-4 py-5 text-xs text-muted-foreground sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <p>Ledgerline · Payment reconciliation</p>
          <p>Support: contact your workspace administrator.</p>
        </div>
      </div>
    </footer>
  )
}
