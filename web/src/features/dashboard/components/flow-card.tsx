import { ArrowRight, CreditCard, GitCompareArrows, Inbox, ShoppingCart } from 'lucide-react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import type { DashboardFlow } from '@/types/api'

const steps = [
  { key: 'ordersCreated', label: 'Orders', icon: ShoppingCart },
  { key: 'successfulPayments', label: 'Successful payments', icon: CreditCard },
  { key: 'matched', label: 'Matched payments', icon: GitCompareArrows },
  { key: 'unmatched', label: 'In review', icon: Inbox },
] as const

export function FlowCard({ flow }: { flow: DashboardFlow }) {
  return (
    <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <div>
            <CardTitle className="text-base">Reconciliation flow</CardTitle>
            <CardDescription>All-time totals for orders and the payments that settle them.</CardDescription>
          </div>
          <p className="text-[11px] text-muted-foreground">
            Webhooks: <span className="font-medium text-foreground">{flow.webhooksReceived}</span> · Recorded:{' '}
            <span className="font-medium text-foreground">{flow.paymentsRecorded}</span> · Manual:{' '}
            <span className="font-medium text-foreground">{flow.manualMatches}</span>
          </p>
        </div>
      </CardHeader>
      <CardContent>
        <div className="flex flex-col gap-2 sm:flex-row sm:items-stretch">
          {steps.map((step, index) => (
            <div key={step.key} className="flex flex-1 items-center gap-2">
              <div className="flex-1 rounded-lg border bg-muted/30 px-3 py-3">
                <div className="flex items-center gap-2 text-muted-foreground">
                  <span className="flex size-7 items-center justify-center rounded-md border bg-card text-primary">
                    <step.icon className="size-3.5" aria-hidden />
                  </span>
                  <p className="text-[11px] font-medium">{step.label}</p>
                </div>
                <p className="mt-2 text-2xl font-semibold tracking-tight tabular-nums">{flow[step.key]}</p>
              </div>
              {index < steps.length - 1 ? (
                <ArrowRight className="hidden size-4 shrink-0 text-muted-foreground/60 sm:block" aria-hidden />
              ) : null}
            </div>
          ))}
        </div>

        <p className="mt-4 rounded-lg border border-dashed bg-background px-3 py-2 text-xs text-muted-foreground">
          Every accepted webhook records a payment, attempts a deterministic match, then writes the outcome and an audit
          entry in the same database transaction.
        </p>
      </CardContent>
    </Card>
  )
}
