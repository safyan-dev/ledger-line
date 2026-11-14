import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Building2, CreditCard } from 'lucide-react'
import { Link } from 'react-router-dom'
import { DetailSection } from '@/components/data/detail-section'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { StatCard } from '@/components/data/stat-card'
import { StatusBadge } from '@/components/data/status-badge'
import { PageHeader } from '@/components/layout/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { getPlatformDashboard } from '@/features/platform/api'
import { errorMessage } from '@/lib/api-client'
import { formatDate } from '@/lib/format'

export function PlatformDashboardPage() {
  const dashboard = useQuery({
    queryKey: ['platform', 'dashboard'],
    queryFn: getPlatformDashboard,
  })

  if (dashboard.isError) {
    return (
      <div>
        <PageHeader title="Platform dashboard" description="A current overview of tenant and subscription operations." />
        <ErrorState message={errorMessage(dashboard.error)} onRetry={() => void dashboard.refetch()} />
      </div>
    )
  }

  if (dashboard.isLoading || !dashboard.data) {
    return (
      <div className="space-y-6" aria-busy="true" aria-label="Loading platform dashboard">
        <PageHeader title="Platform dashboard" description="A current overview of tenant and subscription operations." />
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
          {Array.from({ length: 5 }).map((_, index) => (
            <Skeleton key={index} className="h-28 rounded-xl" />
          ))}
        </div>
        <div className="grid gap-4 xl:grid-cols-2">
          <Skeleton className="h-72 rounded-xl" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      </div>
    )
  }

  const data = dashboard.data

  return (
    <div>
      <PageHeader
        title="Platform dashboard"
        description="A current overview of tenant and subscription operations."
        actions={
          <Button asChild variant="outline" size="sm">
            <Link to="/platform/tenants">View tenants</Link>
          </Button>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard label="Total tenants" value={data.totalTenants} />
        <StatCard label="Active tenants" value={data.activeTenants} />
        <StatCard label="Suspended tenants" value={data.suspendedTenants} />
        <StatCard label="Active subscriptions" value={data.activeSubscriptions} />
        <StatCard label="Trial subscriptions" value={data.trialSubscriptions} />
      </div>

      <div className="mt-6 grid gap-4 xl:grid-cols-2">
        <DetailSection title="Recent registrations" description="The latest workspaces created on the platform.">
          {data.recentRegistrations.length === 0 ? (
            <EmptyState
              icon={<Building2 className="size-4" aria-hidden />}
              title="No tenant registrations"
              description="New workspaces will appear here."
            />
          ) : (
            <ul className="divide-y divide-border/70">
              {data.recentRegistrations.map((tenant) => (
                <li key={tenant.id} className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0 last:pb-0">
                  <div className="min-w-0">
                    <Link
                      to={`/platform/tenants/${tenant.id}`}
                      className="truncate text-sm font-medium text-foreground hover:text-primary"
                    >
                      {tenant.name}
                    </Link>
                    <p className="mt-0.5 truncate text-xs text-muted-foreground">
                      {tenant.ownerEmail ?? 'Owner email unavailable'}
                      {tenant.planName ? ` · ${tenant.planName}` : ''}
                    </p>
                  </div>
                  <span className="shrink-0 text-xs text-muted-foreground">{formatDate(tenant.createdAt)}</span>
                </li>
              ))}
            </ul>
          )}
        </DetailSection>

        <DetailSection
          title="Subscriptions requiring attention"
          description="Past-due, suspended, and trials ending in the next seven days."
        >
          {data.subscriptionsRequiringAttention.length === 0 ? (
            <EmptyState
              icon={<CreditCard className="size-4" aria-hidden />}
              title="Nothing needs attention"
              description="Subscriptions requiring review will be listed here."
            />
          ) : (
            <ul className="divide-y divide-border/70">
              {data.subscriptionsRequiringAttention.map((subscription) => (
                <li
                  key={subscription.id}
                  className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0 last:pb-0"
                >
                  <div className="flex min-w-0 items-center gap-3">
                    <AlertTriangle className="size-4 shrink-0 text-warning" aria-hidden />
                    <div className="min-w-0">
                      <Link
                        to={`/platform/tenants/${subscription.tenantId}`}
                        className="truncate text-sm font-medium text-foreground hover:text-primary"
                      >
                        {subscription.tenantName}
                      </Link>
                      <p className="mt-0.5 truncate text-xs text-muted-foreground">{subscription.planName}</p>
                    </div>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    <StatusBadge tone={subscription.status === 'PastDue' ? 'danger' : 'warning'}>
                      {subscription.status}
                    </StatusBadge>
                    {subscription.currentPeriodEnd ? (
                      <Badge variant="outline">{formatDate(subscription.currentPeriodEnd)}</Badge>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </DetailSection>
      </div>
    </div>
  )
}
