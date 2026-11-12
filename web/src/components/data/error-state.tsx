import { AlertTriangle, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

export function ErrorState({
  message,
  onRetry,
  className,
}: {
  message?: string
  onRetry?: () => void
  className?: string
}) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-3 rounded-xl border border-destructive/20 bg-destructive/5 px-6 py-12 text-center',
        className,
      )}
      role="alert"
    >
      <div className="flex size-10 items-center justify-center rounded-lg bg-destructive/10 text-destructive">
        <AlertTriangle className="size-5" aria-hidden />
      </div>
      <div>
        <p className="text-sm font-medium text-foreground">Could not load this data</p>
        <p className="mt-1 max-w-md text-xs text-muted-foreground">{message ?? 'The API did not respond as expected.'}</p>
      </div>
      {onRetry ? (
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RotateCcw className="size-3.5" aria-hidden />
          Try again
        </Button>
      ) : null}
    </div>
  )
}
