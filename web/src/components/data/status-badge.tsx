import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'
import type { Tone } from '@/lib/status'

const tones: Record<Tone, string> = {
  success: 'border-success/20 bg-success/10 text-success',
  info: 'border-primary/20 bg-primary/10 text-primary',
  warning: 'border-warning/30 bg-warning/15 text-warning-foreground',
  danger: 'border-destructive/20 bg-destructive/10 text-destructive',
  neutral: 'border-border bg-muted text-muted-foreground',
  outline: 'border-border bg-transparent text-muted-foreground',
}

export function StatusBadge({
  tone,
  children,
  className,
}: {
  tone: Tone
  children: ReactNode
  className?: string
}) {
  return (
    <span
      className={cn(
        'inline-flex w-fit shrink-0 items-center justify-center gap-1.5 whitespace-nowrap rounded-md border px-2 py-0.5 text-xs font-medium',
        tones[tone],
        className,
      )}
    >
      <span className="size-1.5 rounded-full bg-current" aria-hidden />
      {children}
    </span>
  )
}
