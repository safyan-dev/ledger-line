import { Link } from 'react-router-dom'
import { ShieldAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'

export function AccessDenied({
  title = 'Access restricted',
  description = 'Your role does not allow access to this section.',
}: {
  title?: string
  description?: string
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-4 rounded-xl border border-border/80 bg-card px-6 py-16 text-center shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <div className="flex size-11 items-center justify-center rounded-full bg-destructive/10 text-destructive">
        <ShieldAlert className="size-5" aria-hidden />
      </div>
      <div className="space-y-1">
        <h2 className="text-lg font-semibold tracking-tight text-foreground">{title}</h2>
        <p className="max-w-md text-sm text-muted-foreground">{description}</p>
      </div>
      <Button asChild variant="outline">
        <Link to="/dashboard">Back to dashboard</Link>
      </Button>
    </div>
  )
}
