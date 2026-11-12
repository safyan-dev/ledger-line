import { Money } from '@/components/data/money'
import { StatusBadge } from '@/components/data/status-badge'
import { cn } from '@/lib/utils'
import { reconciliationOutcomeMeta } from '@/lib/status'

export function AmountComparison({
  expectedAmount,
  expectedCurrency,
  actualAmount,
  actualCurrency,
  outcome,
  reason,
  className,
}: {
  expectedAmount: number | null | undefined
  expectedCurrency?: string | null
  actualAmount: number
  actualCurrency: string
  outcome?: string | null
  reason?: string | null
  className?: string
}) {
  const hasExpected = expectedAmount !== null && expectedAmount !== undefined
  const difference = hasExpected ? actualAmount - expectedAmount : null
  const currencyMismatch = Boolean(expectedCurrency && expectedCurrency !== actualCurrency)

  return (
    <div className={cn('space-y-3', className)}>
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="rounded-lg border bg-muted/25 p-3">
          <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Internal order</p>
          <p className="mt-1.5 text-lg font-semibold tabular-nums text-foreground">
            {hasExpected ? <Money amount={expectedAmount} currency={expectedCurrency ?? actualCurrency} /> : '—'}
          </p>
          <p className="mt-1 text-[11px] text-muted-foreground">
            {hasExpected ? expectedCurrency ?? actualCurrency : 'No linked order'}
          </p>
        </div>
        <div
          className={cn(
            'rounded-lg border p-3',
            currencyMismatch ? 'border-destructive/30 bg-destructive/5' : 'bg-muted/25',
          )}
        >
          <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Provider payment</p>
          <p className="mt-1.5 text-lg font-semibold tabular-nums text-foreground">
            <Money amount={actualAmount} currency={actualCurrency} />
          </p>
          <p className="mt-1 text-[11px] text-muted-foreground">{actualCurrency}</p>
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-dashed px-3 py-2">
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span>Difference</span>
          {difference === null ? (
            <span className="font-medium text-foreground">—</span>
          ) : (
            <span
              className={cn(
                'font-semibold tabular-nums',
                difference === 0 ? 'text-success' : 'text-warning-foreground',
              )}
            >
              <Money amount={difference} currency={actualCurrency} />
            </span>
          )}
        </div>
        {outcome ? (
          <StatusBadge tone={reconciliationOutcomeMeta(outcome).tone}>
            {reconciliationOutcomeMeta(outcome).label}
          </StatusBadge>
        ) : null}
      </div>

      {currencyMismatch ? (
        <p className="text-xs text-destructive">
          Currency mismatch: order is {expectedCurrency}, payment is {actualCurrency}.
        </p>
      ) : null}
      {reason ? <p className="text-xs text-muted-foreground">{reason}</p> : null}
    </div>
  )
}
