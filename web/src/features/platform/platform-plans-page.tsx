import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, CircleDollarSign, Settings2, X } from 'lucide-react'
import { toast } from 'sonner'
import { ConfirmDialog } from '@/components/data/confirm-dialog'
import { DetailSection } from '@/components/data/detail-section'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { StatusBadge } from '@/components/data/status-badge'
import { PageHeader } from '@/components/layout/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import {
  getPlatformPlans,
  updatePlatformPlanFeature,
  updatePlatformPlanLimit,
  updatePlatformPlanStatus,
} from '@/features/platform/api'
import { errorMessage } from '@/lib/api-client'
import type { PlatformPlanDto } from '@/types/api'

function LimitEditor({
  planCode,
  limit,
  onSave,
  disabled,
}: {
  planCode: string
  limit: PlatformPlanDto['limits'][number]
  onSave: (planCode: string, key: string, value: number | null) => void
  disabled: boolean
}) {
  const [value, setValue] = useState(limit.value === null ? '' : String(limit.value))
  const hasChanged = value !== (limit.value === null ? '' : String(limit.value))

  return (
    <form
      className="flex flex-wrap items-end gap-2"
      onSubmit={(event) => {
        event.preventDefault()
        const parsed = value === '' ? null : Number(value)
        if (parsed !== null && (!Number.isInteger(parsed) || parsed < 0)) {
          toast.error('Enter a non-negative whole-number limit, or leave it empty for unlimited.')
          return
        }
        onSave(planCode, limit.key, parsed)
      }}
    >
      <label className="min-w-36 flex-1 text-xs text-muted-foreground">
        {limit.label}
        <Input
          type="number"
          min={0}
          step={1}
          value={value}
          onChange={(event) => setValue(event.target.value)}
          className="mt-1"
          aria-label={`${limit.label} limit`}
        />
      </label>
      <Button type="submit" size="sm" variant="outline" disabled={disabled || !hasChanged}>
        Save
      </Button>
      <span className="pb-2 text-[11px] text-muted-foreground">{limit.display}</span>
    </form>
  )
}

export function PlatformPlansPage() {
  const queryClient = useQueryClient()
  const [planToToggle, setPlanToToggle] = useState<PlatformPlanDto | null>(null)
  const plans = useQuery({ queryKey: ['platform', 'plans'], queryFn: getPlatformPlans })

  const refreshPlans = () => queryClient.invalidateQueries({ queryKey: ['platform', 'plans'] })
  const statusMutation = useMutation({
    mutationFn: ({ code, isActive }: { code: string; isActive: boolean }) =>
      updatePlatformPlanStatus(code, isActive),
    onSuccess: async (_result, variables) => {
      toast.success(variables.isActive ? 'Plan activated.' : 'Plan deactivated.')
      setPlanToToggle(null)
      await refreshPlans()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })
  const featureMutation = useMutation({
    mutationFn: ({ code, key, included }: { code: string; key: string; included: boolean }) =>
      updatePlatformPlanFeature(code, key, included),
    onSuccess: async (_result, variables) => {
      toast.success(`${variables.key} ${variables.included ? 'enabled' : 'disabled'} for this plan.`)
      await refreshPlans()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })
  const limitMutation = useMutation({
    mutationFn: ({ code, key, value }: { code: string; key: string; value: number | null }) =>
      updatePlatformPlanLimit(code, key, value),
    onSuccess: async () => {
      toast.success('Plan usage limit updated.')
      await refreshPlans()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  if (plans.isError) {
    return (
      <div>
        <PageHeader title="Plans" description="Review plans and maintain feature access and usage limits." />
        <ErrorState message={errorMessage(plans.error)} onRetry={() => void plans.refetch()} />
      </div>
    )
  }

  if (plans.isLoading || !plans.data) {
    return (
      <div className="space-y-5" aria-busy="true" aria-label="Loading plans">
        <PageHeader title="Plans" description="Review plans and maintain feature access and usage limits." />
        <div className="grid gap-4 xl:grid-cols-2">
          <Skeleton className="h-[520px] rounded-xl" />
          <Skeleton className="h-[520px] rounded-xl" />
        </div>
      </div>
    )
  }

  return (
    <div>
      <PageHeader
        title="Plans"
        description="Review plans and maintain feature access and usage limits."
        actions={<Badge variant="outline">Plan creation and metadata editing are not supported</Badge>}
      />

      {plans.data.length === 0 ? (
        <EmptyState
          icon={<CircleDollarSign className="size-4" aria-hidden />}
          title="No plans configured"
          description="Plans will appear here when the backend catalog is configured."
        />
      ) : (
        <div className="grid gap-4 xl:grid-cols-2">
          {plans.data.map((plan) => (
            <DetailSection
              key={plan.code}
              title={plan.name}
              description={`${plan.tagline} · ${plan.audience}`}
              actions={
                <>
                  <StatusBadge tone={plan.isActive ? 'success' : 'neutral'}>
                    {plan.isActive ? 'Active' : 'Inactive'}
                  </StatusBadge>
                  <Button
                    variant={plan.isActive ? 'outline' : 'default'}
                    size="sm"
                    onClick={() => setPlanToToggle(plan)}
                  >
                    {plan.isActive ? 'Deactivate' : 'Activate'}
                  </Button>
                </>
              }
            >
              <div className="mb-5 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                <span className="font-medium text-foreground">{plan.priceNote}</span>
                {plan.monthlyPrice !== null ? <span>(${plan.monthlyPrice} / month)</span> : null}
                <span className="font-mono text-xs">{plan.code}</span>
              </div>

              <div className="grid gap-5 md:grid-cols-2">
                <section>
                  <h3 className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    <Settings2 className="size-3.5" aria-hidden />
                    Feature availability
                  </h3>
                  <ul className="space-y-2">
                    {plan.features.map((feature) => (
                      <li key={feature.key} className="flex items-center justify-between gap-3 rounded-lg border p-2.5">
                        <div className="min-w-0">
                          <p className="truncate text-sm font-medium text-foreground">{feature.label}</p>
                          {feature.note ? <p className="text-[11px] text-muted-foreground">{feature.note}</p> : null}
                        </div>
                        <Button
                          type="button"
                          size="sm"
                          variant={feature.included ? 'secondary' : 'outline'}
                          aria-pressed={feature.included}
                          disabled={!plan.isActive}
                          loading={
                            featureMutation.isPending &&
                            featureMutation.variables?.code === plan.code &&
                            featureMutation.variables?.key === feature.key
                          }
                          onClick={() =>
                            featureMutation.mutate({
                              code: plan.code,
                              key: feature.key,
                              included: !feature.included,
                            })
                          }
                        >
                          {feature.included ? <Check className="size-3.5" aria-hidden /> : <X className="size-3.5" aria-hidden />}
                          {feature.included ? 'Included' : 'Not included'}
                        </Button>
                      </li>
                    ))}
                  </ul>
                </section>

                <section>
                  <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    Usage limits
                  </h3>
                  <div className="space-y-3">
                    {plan.limits.map((limit) => (
                      <LimitEditor
                        key={`${plan.code}-${limit.key}-${limit.value}`}
                        planCode={plan.code}
                        limit={limit}
                        disabled={limitMutation.isPending || !plan.isActive}
                        onSave={(code, key, value) => limitMutation.mutate({ code, key, value })}
                      />
                    ))}
                  </div>
                </section>
              </div>
            </DetailSection>
          ))}
        </div>
      )}

      <ConfirmDialog
        open={planToToggle !== null}
        onOpenChange={(open) => {
          if (!open) setPlanToToggle(null)
        }}
        title={planToToggle?.isActive ? 'Deactivate this plan?' : 'Activate this plan?'}
        description={
          planToToggle?.isActive
            ? 'New tenants will not be able to select this plan. Existing subscriptions will not be changed.'
            : 'This plan will become available for new tenant subscriptions.'
        }
        confirmLabel={planToToggle?.isActive ? 'Deactivate plan' : 'Activate plan'}
        destructive={Boolean(planToToggle?.isActive)}
        pending={statusMutation.isPending}
        onConfirm={() => {
          if (planToToggle) {
            statusMutation.mutate({ code: planToToggle.code, isActive: !planToToggle.isActive })
          }
        }}
      />
    </div>
  )
}
