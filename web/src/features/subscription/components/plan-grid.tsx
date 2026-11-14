import { ArrowDownRight, ArrowUpRight, Check, Minus } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'
import type { Plan } from '@/public-site/api'

export function PlanGrid({
  plans,
  currentPlanCode,
  pendingPlanCode,
  canManage,
  onSelect,
}: {
  plans: Plan[]
  currentPlanCode: string
  pendingPlanCode: string | null
  canManage: boolean
  onSelect: (plan: Plan) => void
}) {
  const currentPlan = plans.find((plan) => plan.code === currentPlanCode)
  const currentSortOrder = currentPlan?.sortOrder ?? 0
  const ordered = [...plans].sort((left, right) => left.sortOrder - right.sortOrder)

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      {ordered.map((plan) => {
        const isCurrent = plan.code === currentPlanCode
        const isPending = plan.code === pendingPlanCode
        const isUpgrade = plan.sortOrder > currentSortOrder
        const included = plan.features.filter((feature) => feature.included)
        const excluded = plan.features.filter((feature) => !feature.included)

        return (
          <Card
            key={plan.code}
            className={cn(
              'flex flex-col border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]',
              isCurrent && 'border-primary/50 ring-1 ring-primary/20',
            )}
          >
            <CardHeader className="pb-2">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <CardTitle className="flex items-center gap-2 text-base">
                    {plan.name}
                    {plan.recommended ? <Badge variant="secondary">Recommended</Badge> : null}
                  </CardTitle>
                  <CardDescription className="mt-1">{plan.tagline}</CardDescription>
                </div>
                {isCurrent ? <Badge>Current</Badge> : null}
                {isPending ? <Badge variant="outline">Scheduled</Badge> : null}
              </div>
            </CardHeader>
            <CardContent className="flex flex-1 flex-col gap-4">
              <div>
                <p className="text-lg font-semibold text-foreground">{plan.priceNote}</p>
                <p className="text-xs text-muted-foreground">{plan.audience}</p>
              </div>

              <dl className="grid grid-cols-2 gap-2 border-y border-border/60 py-3">
                {plan.limits.map((limit) => (
                  <div key={limit.key}>
                    <dt className="text-[11px] text-muted-foreground">{limit.label}</dt>
                    <dd className="text-sm font-medium text-foreground">{limit.display}</dd>
                  </div>
                ))}
              </dl>

              <ul className="space-y-1.5">
                {included.map((feature) => (
                  <li key={feature.key} className="flex items-start gap-2 text-xs text-foreground">
                    <Check className="mt-0.5 size-3.5 shrink-0 text-success" aria-hidden />
                    {feature.label}
                  </li>
                ))}
                {excluded.map((feature) => (
                  <li key={feature.key} className="flex items-start gap-2 text-xs text-muted-foreground">
                    <Minus className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                    {feature.label}
                  </li>
                ))}
              </ul>

              <div className="mt-auto">
                {isCurrent ? (
                  <Button type="button" variant="outline" className="w-full" disabled>
                    Current plan
                  </Button>
                ) : (
                  <Button
                    type="button"
                    className="w-full"
                    variant={isUpgrade ? 'default' : 'outline'}
                    disabled={!canManage || isPending}
                    onClick={() => onSelect(plan)}
                  >
                    {isUpgrade ? <ArrowUpRight className="size-4" aria-hidden /> : <ArrowDownRight className="size-4" aria-hidden />}
                    {isPending ? 'Change scheduled' : isUpgrade ? 'Upgrade now' : 'Schedule downgrade'}
                  </Button>
                )}
                {!canManage ? (
                  <p className="mt-2 text-center text-[11px] text-muted-foreground">
                    Only the workspace owner can change plans.
                  </p>
                ) : null}
              </div>
            </CardContent>
          </Card>
        )
      })}
    </div>
  )
}
