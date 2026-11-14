import { Link } from 'react-router-dom'
import { CircleAlert, Gauge, Sparkles } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { cn } from '@/lib/utils'
import type { UsageMetricDto } from '@/types/api'

export function UsageCard({
  metric,
  canManageSubscription,
}: {
  metric: UsageMetricDto
  canManageSubscription: boolean
}) {
  const limit = metric.limit
  const percentage =
    limit === null ? null : limit <= 0 ? 100 : Math.min(100, Math.round((metric.used / limit) * 100))
  const remaining = limit === null ? null : Math.max(0, limit - metric.used)
  const reached = limit !== null && metric.used >= limit
  const warning = limit !== null && !reached && percentage !== null && percentage >= 80

  return (
    <Card
      className={cn(
        'border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]',
        reached && 'border-destructive/40',
        warning && 'border-warning/40',
      )}
    >
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2 text-base">
              <Gauge className="size-4 text-primary" aria-hidden />
              {metric.label}
            </CardTitle>
            <CardDescription>{metric.period === 'current' ? 'Current active members' : `Usage for ${metric.period}`}</CardDescription>
          </div>
          <Badge variant={reached ? 'destructive' : warning ? 'secondary' : 'outline'}>
            {percentage === null ? 'Unlimited' : `${percentage}%`}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex items-end justify-between gap-3">
          <p className="text-2xl font-semibold tabular-nums text-foreground">
            {metric.used}
            {limit === null ? null : <span className="text-sm font-normal text-muted-foreground"> / {limit}</span>}
          </p>
          <p className="text-xs text-muted-foreground">
            {remaining === null ? 'No limit' : `${remaining} remaining`}
          </p>
        </div>

        {percentage === null ? null : <Progress value={percentage} />}

        {reached ? (
          <p className="flex items-start gap-1.5 text-xs text-destructive">
            <CircleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Limit reached. New {metric.label.toLowerCase()} are blocked until the plan is upgraded or capacity is freed.
          </p>
        ) : warning ? (
          <p className="flex items-start gap-1.5 text-xs text-warning-foreground">
            <CircleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Approaching the plan limit.
          </p>
        ) : null}

        {canManageSubscription ? (
          <Button asChild variant="outline" size="sm">
            <Link to="/settings/subscription">
              <Sparkles className="size-3.5" aria-hidden />
              Upgrade plan
            </Link>
          </Button>
        ) : (
          <p className="text-xs text-muted-foreground">Ask the workspace owner to increase the limit.</p>
        )}
      </CardContent>
    </Card>
  )
}
