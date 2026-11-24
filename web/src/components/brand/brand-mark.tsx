import { cn } from '@/lib/utils'

// Ledgerline mark: an L (the setter of the ledger) whose foot is the ledger rule,
// with two posted entries to its right. Drawn on a 32-unit grid so it stays crisp
// at favicon and lockup sizes.
export function BrandLogo({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 32 32"
      className={cn('size-8', className)}
      role="presentation"
      aria-hidden="true"
      focusable="false"
    >
      <rect width="32" height="32" rx="8" className="fill-primary" />
      <g fill="none" stroke="currentColor" strokeLinecap="round" className="text-primary-foreground">
        <path d="M11.9 8.8V21" strokeWidth="3" />
        <path d="M11.9 21h11.4" strokeWidth="3" />
        <path d="M17.3 12.3h4.6" strokeWidth="2.4" />
        <path d="M17.3 16.9h2.4" strokeWidth="2.4" />
      </g>
    </svg>
  )
}

export function BrandMark({ className, subtitle = true }: { className?: string; subtitle?: boolean }) {
  return (
    <div className={cn('flex items-center gap-2.5', className)}>
      <BrandLogo className="size-8 shadow-sm" />
      <div className="leading-none">
        <p className="text-[15px] font-semibold tracking-tight text-foreground">Ledgerline</p>
        {subtitle ? (
          <p className="mt-1 text-[11px] font-medium text-muted-foreground">Payment reconciliation</p>
        ) : null}
      </div>
    </div>
  )
}
