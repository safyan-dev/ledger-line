import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export function DetailList({ children, className }: { children: ReactNode; className?: string }) {
  return <dl className={cn('min-w-0 divide-y divide-border/70', className)}>{children}</dl>
}

export function DetailRow({
  label,
  children,
  className,
}: {
  label: string
  children: ReactNode
  className?: string
}) {
  return (
    <div className={cn('flex min-w-0 items-start justify-between gap-6 py-2.5', className)}>
      <dt className="shrink-0 text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="min-w-0 break-words text-right text-sm font-medium text-foreground">{children}</dd>
    </div>
  )
}
