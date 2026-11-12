import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export type TimelineItem = {
  id: string
  title: ReactNode
  description?: ReactNode
  timestamp?: ReactNode
  tone?: 'success' | 'info' | 'warning' | 'danger' | 'neutral'
}

const dotTones: Record<NonNullable<TimelineItem['tone']>, string> = {
  success: 'bg-success',
  info: 'bg-primary',
  warning: 'bg-warning',
  danger: 'bg-destructive',
  neutral: 'bg-slate-400',
}

export function Timeline({ items, className }: { items: TimelineItem[]; className?: string }) {
  return (
    <ol className={cn('relative space-y-0', className)}>
      {items.map((item, index) => (
        <li key={item.id} className="relative flex gap-3 pb-4 last:pb-0">
          {index < items.length - 1 ? (
            <span className="absolute left-[5px] top-3 h-full w-px bg-border" aria-hidden />
          ) : null}
          <span
            className={cn('relative mt-1.5 size-2.5 shrink-0 rounded-full ring-4 ring-card', dotTones[item.tone ?? 'neutral'])}
            aria-hidden
          />
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <p className="text-sm font-medium text-foreground">{item.title}</p>
              {item.timestamp ? <p className="text-[11px] text-muted-foreground">{item.timestamp}</p> : null}
            </div>
            {item.description ? <div className="mt-0.5 text-xs text-muted-foreground">{item.description}</div> : null}
          </div>
        </li>
      ))}
    </ol>
  )
}
