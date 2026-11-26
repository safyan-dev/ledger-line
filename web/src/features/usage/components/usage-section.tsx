import { useQuery } from '@tanstack/react-query'
import { Check, Minus } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { ErrorState } from '@/components/data/error-state'
import { UsageCard } from '@/features/usage/components/usage-card'
import { getUsage } from '@/features/usage/api'
import { errorMessage } from '@/lib/api-client'
import { usePermissions } from '@/lib/permissions'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'

export function UsageSection() {
  const tenantId = useTenantKey()
  const { canManageSubscription } = usePermissions()
  const usage = useQuery({ queryKey: qk.usage(tenantId), queryFn: getUsage })

  if (usage.isError) {
    return <ErrorState message={errorMessage(usage.error)} onRetry={() => void usage.refetch()} />
  }

  if (usage.isLoading || !usage.data) {
    return (
      <div className="space-y-4" aria-busy="true" aria-label="Loading usage">
        <div className="grid gap-4 md:grid-cols-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <Skeleton key={index} className="h-48 w-full rounded-xl" />
          ))}
        </div>
        <Skeleton className="h-40 w-full rounded-xl" />
      </div>
    )
  }

  const data = usage.data

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold tracking-tight text-foreground">Usage this period</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Authoritative usage from the backend for the {data.planName} plan.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        {data.metrics.map((metric) => (
          <UsageCard key={metric.key} metric={metric} canManageSubscription={canManageSubscription} />
        ))}
      </div>

      <div className="rounded-xl border border-border/80 bg-card p-4 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <h3 className="text-sm font-semibold text-foreground">Plan features</h3>
        <ul className="mt-3 grid gap-2 sm:grid-cols-2">
          {data.features.map((feature) => (
            <li key={feature.key} className="flex items-start gap-2 text-sm">
              {feature.included ? (
                <Check className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
              ) : (
                <Minus className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
              )}
              <span className={feature.included ? 'text-foreground' : 'text-muted-foreground'}>
                {feature.label}
                {feature.note ? <span className="block text-xs text-muted-foreground">{feature.note}</span> : null}
              </span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
