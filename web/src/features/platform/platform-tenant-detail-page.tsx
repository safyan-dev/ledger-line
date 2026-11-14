import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Building2 } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { ConfirmDialog } from '@/components/data/confirm-dialog'
import { DetailList, DetailRow } from '@/components/data/detail-list'
import { DetailSection } from '@/components/data/detail-section'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { StatusBadge } from '@/components/data/status-badge'
import { PageHeader } from '@/components/layout/page-header'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { getPlatformTenant, updatePlatformTenantStatus } from '@/features/platform/api'
import { errorMessage } from '@/lib/api-client'
import { formatDate } from '@/lib/format'

export function PlatformTenantDetailPage() {
  const { tenantId = '' } = useParams()
  const queryClient = useQueryClient()
  const [confirmOpen, setConfirmOpen] = useState(false)
  const tenantQuery = useQuery({
    queryKey: ['platform', 'tenant', tenantId],
    queryFn: () => getPlatformTenant(tenantId),
    enabled: Boolean(tenantId),
  })
  const nextStatus = tenantQuery.data?.status === 'Suspended' ? 'Active' : 'Suspended'
  const statusMutation = useMutation({
    mutationFn: () => updatePlatformTenantStatus(tenantId, nextStatus),
    onSuccess: async () => {
      toast.success(nextStatus === 'Suspended' ? 'Tenant suspended.' : 'Tenant reactivated.')
      setConfirmOpen(false)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['platform', 'tenant', tenantId] }),
        queryClient.invalidateQueries({ queryKey: ['platform', 'tenants'] }),
        queryClient.invalidateQueries({ queryKey: ['platform', 'dashboard'] }),
      ])
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  if (tenantQuery.isError) {
    return (
      <div>
        <PageHeader title="Tenant details" description="Workspace information and operational status." />
        <ErrorState message={errorMessage(tenantQuery.error)} onRetry={() => void tenantQuery.refetch()} />
      </div>
    )
  }

  if (tenantQuery.isLoading || !tenantQuery.data) {
    return (
      <div className="space-y-5" aria-busy="true" aria-label="Loading tenant details">
        <Skeleton className="h-10 w-80" />
        <div className="grid gap-4 lg:grid-cols-2">
          <Skeleton className="h-72 rounded-xl" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      </div>
    )
  }

  const tenant = tenantQuery.data
  const tone = tenant.status === 'Active' ? 'success' : tenant.status === 'Suspended' ? 'danger' : 'neutral'

  return (
    <div>
      <PageHeader
        title={tenant.name}
        description={`Workspace details for ${tenant.slug}.`}
        actions={
          <>
            <StatusBadge tone={tone}>{tenant.status}</StatusBadge>
            <Button variant="outline" size="sm" asChild>
              <Link to="/platform/tenants">
                <ArrowLeft className="size-3.5" aria-hidden />
                Tenants
              </Link>
            </Button>
            <Button
              variant={nextStatus === 'Suspended' ? 'destructive' : 'default'}
              size="sm"
              onClick={() => setConfirmOpen(true)}
            >
              {nextStatus === 'Suspended' ? 'Suspend tenant' : 'Reactivate tenant'}
            </Button>
          </>
        }
      />

      <div className="grid gap-4 xl:grid-cols-2">
        <DetailSection title="Workspace information" description="Tenant settings and registration information.">
          <DetailList>
            <DetailRow label="Workspace name">{tenant.name}</DetailRow>
            <DetailRow label="Slug">{tenant.slug}</DetailRow>
            <DetailRow label="Status"><StatusBadge tone={tone}>{tenant.status}</StatusBadge></DetailRow>
            <DetailRow label="Business contact">{tenant.businessContactEmail ?? 'Not provided'}</DetailRow>
            <DetailRow label="Default currency">{tenant.defaultCurrency}</DetailRow>
            <DetailRow label="Time zone">{tenant.timeZone}</DetailRow>
            <DetailRow label="Registered">{formatDate(tenant.createdAt)}</DetailRow>
          </DetailList>
        </DetailSection>

        <DetailSection title="Owner and subscription" description="Account ownership and current subscription state.">
          <DetailList>
            <DetailRow label="Owner">{tenant.owner?.name ?? 'Owner unavailable'}</DetailRow>
            <DetailRow label="Owner email">{tenant.owner?.email ?? 'Not available'}</DetailRow>
            <DetailRow label="Members">{tenant.memberCount}</DetailRow>
            <DetailRow label="Current plan">{tenant.planName ?? 'No subscription'}</DetailRow>
            <DetailRow label="Plan code">{tenant.planCode ?? '—'}</DetailRow>
            <DetailRow label="Subscription status">
              {tenant.subscriptionStatus ? (
                <StatusBadge tone={tenant.subscriptionStatus === 'Active' ? 'success' : 'warning'}>
                  {tenant.subscriptionStatus}
                </StatusBadge>
              ) : 'No subscription'}
            </DetailRow>
          </DetailList>
        </DetailSection>
      </div>

      <div className="mt-4">
        <DetailSection title="Usage" description="Current-period usage totals and plan limits.">
          {tenant.usage ? (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {tenant.usage.metrics.map((metric) => (
                <div key={metric.key} className="rounded-lg border border-border/80 bg-muted/20 p-4">
                  <p className="text-xs font-medium text-muted-foreground">{metric.label}</p>
                  <p className="mt-2 text-lg font-semibold tabular-nums text-foreground">
                    {metric.used}
                    <span className="ml-1 text-sm font-normal text-muted-foreground">
                      / {metric.limit ?? 'Unlimited'}
                    </span>
                  </p>
                  <p className="mt-1 text-[11px] text-muted-foreground">
                    {metric.period === 'current' ? 'Current count' : `Period ${metric.period}`}
                  </p>
                </div>
              ))}
            </div>
          ) : (
            <EmptyState
              icon={<Building2 className="size-4" aria-hidden />}
              title="Usage unavailable"
              description="This tenant does not currently have a subscription to report usage against."
            />
          )}
        </DetailSection>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title={nextStatus === 'Suspended' ? 'Suspend this tenant?' : 'Reactivate this tenant?'}
        description={
          nextStatus === 'Suspended'
            ? 'Workspace members will no longer be able to use this tenant until it is reactivated.'
            : 'Workspace members will regain access to this tenant.'
        }
        confirmLabel={nextStatus === 'Suspended' ? 'Suspend tenant' : 'Reactivate tenant'}
        destructive={nextStatus === 'Suspended'}
        pending={statusMutation.isPending}
        onConfirm={() => statusMutation.mutate()}
      />
    </div>
  )
}
