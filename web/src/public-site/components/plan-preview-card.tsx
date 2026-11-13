import { useNavigate } from 'react-router-dom'
import { ArrowRight, Check, Minus } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import type { Plan } from '@/public-site/api'
import { setSelectedPlan } from '@/lib/session'
import { cn } from '@/lib/utils'

export function PlanPreviewCard({ plan, className }: { plan: Plan; className?: string }) {
  const navigate = useNavigate()
  const included = plan.features.filter((feature) => feature.included)
  const excluded = plan.features.filter((feature) => !feature.included)

  const choose = () => {
    setSelectedPlan({ code: plan.code, name: plan.name })
    navigate(`/sign-up?plan=${encodeURIComponent(plan.code)}`)
  }

  return (
    <Card
      className={cn(
        'h-full gap-0 border-border/80 py-0 shadow-[0_1px_2px_rgba(15,23,42,0.04)]',
        plan.recommended && 'border-primary/40 shadow-[0_12px_40px_-24px_rgba(79,70,229,0.6)]',
        className,
      )}
    >
      <CardContent className="flex h-full flex-col gap-5 p-6">
        <div className="space-y-1.5">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-lg font-semibold tracking-tight text-foreground">{plan.name}</h3>
            {plan.recommended ? <Badge variant="secondary">Recommended</Badge> : null}
          </div>
          <p className="text-sm text-muted-foreground">{plan.tagline}</p>
        </div>

        <div>
          <p className="text-2xl font-semibold tracking-tight text-foreground">{plan.priceNote}</p>
          <p className="mt-1 text-xs text-muted-foreground">{plan.audience}</p>
        </div>

        <dl className="grid grid-cols-2 gap-3 border-y py-4">
          {plan.limits.slice(0, 4).map((limit) => (
            <div key={limit.key}>
              <dt className="text-[11px] uppercase tracking-wide text-muted-foreground">{limit.label}</dt>
              <dd className="mt-0.5 text-sm font-medium text-foreground">{limit.display}</dd>
            </div>
          ))}
        </dl>

        <ul className="space-y-2 text-sm">
          {included.slice(0, 4).map((feature) => (
            <li key={feature.key} className="flex gap-2 text-muted-foreground">
              <Check className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
              <span>{feature.label}</span>
            </li>
          ))}
          {excluded.slice(0, 1).map((feature) => (
            <li key={feature.key} className="flex gap-2 text-muted-foreground/60">
              <Minus className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span>{feature.label}</span>
            </li>
          ))}
        </ul>

        <Button
          className="mt-auto w-full"
          variant={plan.recommended ? 'default' : 'outline'}
          onClick={choose}
        >
          Choose {plan.name}
          <ArrowRight className="size-4" aria-hidden />
        </Button>
      </CardContent>
    </Card>
  )
}
