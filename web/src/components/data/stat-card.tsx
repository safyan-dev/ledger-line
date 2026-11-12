import type { ReactNode } from 'react'
import { Card, CardContent } from '@/components/ui/card'
import { cn } from '@/lib/utils'

export function StatCard({
  label,
  value,
  hint,
  delta,
  children,
  className,
}: {
  label: string
  value: ReactNode
  hint?: ReactNode
  delta?: { value: string; tone?: 'positive' | 'negative' | 'neutral' }
  children?: ReactNode
  className?: string
}) {
  return (
    <Card className={cn('gap-0 border-border/80 py-0 shadow-[0_1px_2px_rgba(15,23,42,0.04)]', className)}>
      <CardContent className="flex items-start justify-between gap-3 px-5 py-4">
        <div className="min-w-0">
          <p className="text-[13px] font-medium text-muted-foreground">{label}</p>
          <div className="mt-2 flex flex-wrap items-baseline gap-2">
            <p className="text-[26px] font-semibold leading-none tracking-tight text-foreground tabular-nums">{value}</p>
            {delta ? (
              <span
                className={cn(
                  'rounded-md px-1.5 py-0.5 text-[11px] font-semibold',
                  delta.tone === 'negative'
                    ? 'bg-destructive/10 text-destructive'
                    : delta.tone === 'neutral'
                      ? 'bg-muted text-muted-foreground'
                      : 'bg-success/10 text-success',
                )}
              >
                {delta.value}
              </span>
            ) : null}
          </div>
          {hint ? <p className="mt-2 text-xs text-muted-foreground">{hint}</p> : null}
        </div>
        {children}
      </CardContent>
    </Card>
  )
}
