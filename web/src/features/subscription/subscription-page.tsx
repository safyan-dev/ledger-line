import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, CircleAlert, CreditCard, Loader2, RotateCcw, Sparkles } from 'lucide-react'
import { toast } from 'sonner'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ConfirmDialog } from '@/components/data/confirm-dialog'
import { ErrorState } from '@/components/data/error-state'
import { StatusBadge } from '@/components/data/status-badge'
import { Timeline } from '@/components/data/timeline'
import { PlanGrid } from '@/features/subscription/components/plan-grid'
import {
  cancelSubscription,
  changeSubscriptionPlan,
  fetchPlans,
  getSubscription,
  getSubscriptionHistory,
  reactivateSubscription,
} from '@/features/subscription/api'
import { UsageSection } from '@/features/usage/components/usage-section'
import { errorMessage } from '@/lib/api-client'
import { formatDate, formatDateTime } from '@/lib/format'
import { usePermissions } from '@/lib/permissions'
import { subscriptionStatusMeta } from '@/lib/status'
import type { Plan } from '@/public-site/api'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'
import { invalidateRelated } from '@/lib/invalidate'

const eventLabels: Record<string, string> = {
  'subscription.activated': 'Plan activated',
  'subscription.upgraded': 'Plan upgraded',
  'subscription.changed': 'Plan changed',
  'subscription.downgrade_scheduled': 'Downgrade scheduled',
  'subscription.downgrade_applied': 'Downgrade applied',
  'subscription.cancel_scheduled': 'Cancellation scheduled',
  'subscription.canceled': 'Subscription canceled',
  'subscription.renewed': 'Subscription renewed',
  'subscription.expired': 'Subscription expired',
  'subscription.reactivated': 'Subscription reactivated',
  'subscription.suspended': 'Subscription suspended',
}

type PendingAction = { type: 'plan'; plan: Plan } | { type: 'cancel' } | null

export function SubscriptionPage() {
  const tenantId = useTenantKey()
  const { canManageSubscription } = usePermissions()
  const queryClient = useQueryClient()
  const [action, setAction] = useState<PendingAction>(null)

  const subscription = useQuery({ queryKey: qk.subscription(tenantId), queryFn: getSubscription })
  const plans = useQuery({ queryKey: ['plans'], queryFn: fetchPlans, staleTime: 5 * 60_000 })
  const history = useQuery({
    queryKey: qk.subscriptionHistory(tenantId, { page: 1, pageSize: 6 }),
    queryFn: () => getSubscriptionHistory({ page: 1, pageSize: 6 }),
  })

  const invalidate = () => {
      invalidateRelated(queryClient, 'subscription')
  }

  const changePlanMutation = useMutation({
    mutationFn: changeSubscriptionPlan,
    onSuccess: (result) => {
      toast.success(`Subscription is now on the ${result.planName} plan.`)
      setAction(null)
      invalidate()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const cancelMutation = useMutation({
    mutationFn: cancelSubscription,
    onSuccess: () => {
      toast.success('Subscription cancellation scheduled.')
      setAction(null)
      invalidate()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const reactivateMutation = useMutation({
    mutationFn: reactivateSubscription,
    onSuccess: () => {
      toast.success('Subscription reactivated.')
      invalidate()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  if (subscription.isError) {
    return <ErrorState message={errorMessage(subscription.error)} onRetry={() => void subscription.refetch()} />
  }

  if (subscription.isLoading || !subscription.data) {
    return (
      <div className="space-y-4" aria-busy="true" aria-label="Loading subscription">
        <Skeleton className="h-56 w-full rounded-xl" />
        <Skeleton className="h-80 w-full rounded-xl" />
        <Skeleton className="h-48 w-full rounded-xl" />
      </div>
    )
  }

  const data = subscription.data
  const status = subscriptionStatusMeta(data.status)
  const isInactive = data.status === 'Expired' || data.status === 'Canceled'
  const canReactivate = data.cancelAtPeriodEnd || isInactive

  const currentPlan = plans.data?.find((plan) => plan.code === data.planCode)
  const pendingPlan = data.pendingPlanCode
    ? plans.data?.find((plan) => plan.code === data.pendingPlanCode)
    : null

  return (
    <div className="space-y-6">
      <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <CardHeader>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <CardTitle className="flex items-center gap-2 text-base">
                <CreditCard className="size-4 text-primary" aria-hidden />
                Current subscription
              </CardTitle>
              <CardDescription>Plan, status and effective dates for this workspace.</CardDescription>
            </div>
            <StatusBadge tone={status.tone}>{status.label}</StatusBadge>
          </div>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <SummaryItem label="Plan" value={data.planName} />
            <SummaryItem label="Activation" value={activationLabel(data.activationSource)} />
            <SummaryItem label="Period started" value={formatDate(data.currentPeriodStart)} />
            <SummaryItem
              label={data.currentPeriodEnd ? 'Renews / ends' : 'Renewal'}
              value={data.currentPeriodEnd ? formatDate(data.currentPeriodEnd) : 'No renewal date'}
            />
          </div>

          {!data.paymentConfirmed && data.activationSource === 'Simulated' ? (
            <Alert>
              <Sparkles className="size-4" aria-hidden />
              <AlertTitle>Simulated activation</AlertTitle>
              <AlertDescription>
                This plan was activated without collecting payment. No payment confirmation is implied.
              </AlertDescription>
            </Alert>
          ) : null}

          {data.pendingPlanCode ? (
            <Alert>
              <CalendarClock className="size-4" aria-hidden />
              <AlertTitle>Plan change scheduled</AlertTitle>
              <AlertDescription>
                The workspace moves to {pendingPlan?.name ?? data.pendingPlanCode}
                {data.pendingPlanEffectiveAt ? ` on ${formatDate(data.pendingPlanEffectiveAt)}` : ' at the end of the current period'}.
              </AlertDescription>
            </Alert>
          ) : null}

          {data.cancelAtPeriodEnd && data.currentPeriodEnd ? (
            <Alert className="border-warning/40">
              <CircleAlert className="size-4 text-warning-foreground" aria-hidden />
              <AlertTitle>Cancellation scheduled</AlertTitle>
              <AlertDescription>
                The subscription ends on {formatDate(data.currentPeriodEnd)}. Existing workspace data is retained.
              </AlertDescription>
            </Alert>
          ) : null}

          {isInactive ? (
            <Alert variant="destructive">
              <CircleAlert className="size-4" aria-hidden />
              <AlertTitle>Subscription {status.label.toLowerCase()}</AlertTitle>
              <AlertDescription>
                Existing data is retained, but new resources may be blocked. Reactivate the workspace to continue.
              </AlertDescription>
            </Alert>
          ) : null}

          {canManageSubscription ? (
            <div className="flex flex-wrap gap-2">
              {!data.cancelAtPeriodEnd && !isInactive ? (
                <Button variant="outline" onClick={() => setAction({ type: 'cancel' })}>
                  Cancel subscription
                </Button>
              ) : null}
              {canReactivate ? (
                <Button onClick={() => reactivateMutation.mutate()} disabled={reactivateMutation.isPending}>
                  {reactivateMutation.isPending ? (
                    <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden />
                  ) : (
                    <RotateCcw className="size-4" aria-hidden />
                  )}
                  Reactivate
                </Button>
              ) : null}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">
              Only the workspace owner can change the subscription.
            </p>
          )}
        </CardContent>
      </Card>

      <div className="space-y-3">
        <div>
          <h2 className="text-lg font-semibold tracking-tight text-foreground">Available plans</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Plan details and limits come from the backend plan catalog.
          </p>
        </div>

        {plans.isError ? (
          <ErrorState message={errorMessage(plans.error)} onRetry={() => void plans.refetch()} />
        ) : plans.isLoading || !plans.data ? (
          <div className="grid gap-4 lg:grid-cols-3" aria-busy="true" aria-label="Loading plans">
            {Array.from({ length: 3 }).map((_, index) => (
              <Skeleton key={index} className="h-80 w-full rounded-xl" />
            ))}
          </div>
        ) : (
          <PlanGrid
            plans={plans.data}
            currentPlanCode={data.planCode}
            pendingPlanCode={data.pendingPlanCode}
            canManage={canManageSubscription}
            onSelect={(plan) => setAction({ type: 'plan', plan })}
          />
        )}
      </div>

      <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <CardHeader>
          <CardTitle className="text-base">Recent subscription activity</CardTitle>
          <CardDescription>History is written when plans, renewals or cancellations change.</CardDescription>
        </CardHeader>
        <CardContent>
          {history.isError ? (
            <ErrorState message={errorMessage(history.error)} onRetry={() => void history.refetch()} />
          ) : history.isLoading || !history.data ? (
            <div className="space-y-3" aria-busy="true" aria-label="Loading subscription history">
              {Array.from({ length: 3 }).map((_, index) => (
                <Skeleton key={index} className="h-12 w-full rounded-lg" />
              ))}
            </div>
          ) : history.data.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No subscription activity yet.</p>
          ) : (
            <Timeline
              items={history.data.items.map((item) => ({
                id: item.id,
                title: eventLabels[item.event] ?? item.event,
                description: [item.planName, item.note].filter(Boolean).join(' · '),
                timestamp: formatDateTime(item.occurredAt),
                tone: item.event.includes('expired') || item.event.includes('canceled') ? 'danger' : 'neutral',
              }))}
            />
          )}
        </CardContent>
      </Card>

      <UsageSection />

      <ConfirmDialog
        open={action !== null}
        onOpenChange={(open) => !open && setAction(null)}
        title={
          action?.type === 'plan'
            ? `Change to ${action.plan.name}?`
            : 'Cancel this subscription?'
        }
        description={
          action?.type === 'plan'
            ? action.plan.sortOrder > (currentPlan?.sortOrder ?? 0)
              ? 'The upgrade applies immediately. No payment is collected in this build.'
              : 'The downgrade takes effect at the end of the current period.'
            : 'Cancellation takes effect at the end of the current period. Workspace data is retained and the owner can reactivate later.'
        }
        confirmLabel={action?.type === 'plan' ? 'Confirm plan change' : 'Cancel subscription'}
        destructive={action?.type === 'cancel'}
        pending={changePlanMutation.isPending || cancelMutation.isPending}
        onConfirm={() => {
          if (action?.type === 'plan') {
            changePlanMutation.mutate(action.plan.code)
          } else if (action?.type === 'cancel') {
            cancelMutation.mutate()
          }
        }}
      />
    </div>
  )
}

function SummaryItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="mt-1 text-sm font-medium text-foreground">{value}</p>
    </div>
  )
}

function activationLabel(source: string): string {
  if (source === 'Free') return 'Free plan'
  if (source === 'Simulated') return 'Simulated activation'
  if (source === 'Manual') return 'Manual activation'
  return source
}
