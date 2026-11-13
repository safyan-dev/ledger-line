import { Check } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import type { Capability } from '@/public-site/content'
import { cn } from '@/lib/utils'

export function FeatureCard({
  capability,
  variant = 'summary',
  className,
}: {
  capability: Capability
  variant?: 'summary' | 'full'
  className?: string
}) {
  const Icon = capability.icon

  return (
    <Card className={cn('gap-0 border-border/80 py-0 shadow-[0_1px_2px_rgba(15,23,42,0.04)]', className)}>
      <CardContent className="flex h-full flex-col gap-4 p-5">
        <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
          <Icon className="size-[18px]" aria-hidden />
        </div>

        <div className="space-y-2">
          <h3 className="text-base font-semibold tracking-tight text-foreground">{capability.title}</h3>
          {variant === 'summary' ? (
            <p className="text-sm leading-6 text-muted-foreground">{capability.solution}</p>
          ) : (
            <div className="space-y-3 text-sm leading-6">
              <p className="text-muted-foreground">
                <span className="font-medium text-foreground">Problem. </span>
                {capability.problem}
              </p>
              <p className="text-muted-foreground">
                <span className="font-medium text-foreground">How it works. </span>
                {capability.solution}
              </p>
              <p className="flex gap-2 text-muted-foreground">
                <Check className="mt-1 size-4 shrink-0 text-success" aria-hidden />
                <span>{capability.value}</span>
              </p>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
