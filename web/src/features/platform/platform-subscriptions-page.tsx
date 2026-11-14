import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CreditCard, ExternalLink } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { ConfirmDialog } from '@/components/data/confirm-dialog'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { DetailList, DetailRow } from '@/components/data/detail-list'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { ListPagination } from '@/components/data/list-pagination'
import { StatusBadge } from '@/components/data/status-badge'
import { PageHeader } from '@/components/layout/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import {
  activatePlatformSubscription,
  changePlatformSubscriptionPlan,
  extendPlatformSubscription,
  getPlatformPlans,
  getPlatformSubscription,
  getPlatformSubscriptions,
  updatePlatformSubscriptionStatus,
} from '@/features/platform/api'
import { errorMessage } from '@/lib/api-client'
import { formatDate } from '@/lib/format'
import { useTableParams } from '@/lib/use-table-params'
import type { PlatformSubscriptionDto } from '@/types/api'

type SubscriptionAction =
  | { type: 'activate' }
  | { type: 'change-plan'; planCode: string }
  | { type: 'extend'; days: number }
  | { type: 'status'; status: 'Active' | 'Suspended' }

function SubscriptionDetailsDialog({
  subscription,
  onClose,
}: {
  subscription: PlatformSubscriptionDto | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [planCode, setPlanCode] = useState('')
  const [days, setDays] = useState('30')
  const [action, setAction] = useState<SubscriptionAction | null>(null)
  const detail = useQuery({
    queryKey: ['platform', 'subscription', subscription?.id],
    queryFn: () => getPlatformSubscription(subscription!.id),
    enabled: Boolean(subscription),
  })
  const plans = useQuery({ queryKey: ['platform', 'plans'], queryFn: getPlatformPlans })

  const mutation = useMutation({
    mutationFn: async (operation: SubscriptionAction) => {
      if (!subscription) throw new Error('Select a subscription first.')
      if (operation.type === 'activate') return activatePlatformSubscription(subscription.id)
      if (operation.type === 'change-plan') {
        return changePlatformSubscriptionPlan(subscription.id, operation.planCode)
      }
      if (operation.type === 'extend') return extendPlatformSubscription(subscription.id, operation.days)
      return updatePlatformSubscriptionStatus(subscription.id, operation.status)
    },
    onSuccess: async (_result, operation) => {
      const message =
        operation.type === 'activate'
          ? 'Subscription manually activated. No payment was processed.'
          : operation.type === 'change-plan'
            ? 'Subscription plan changed manually. No payment was processed.'
            : operation.type === 'extend'
              ? 'Subscription period extended.'
              : operation.status === 'Suspended'
                ? 'Subscription suspended.'
                : 'Subscription reactivated.'
      toast.success(message)
      setAction(null)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['platform', 'subscriptions'] }),
        queryClient.invalidateQueries({ queryKey: ['platform', 'subscription', subscription?.id] }),
        queryClient.invalidateQueries({ queryKey: ['platform', 'dashboard'] }),
        queryClient.invalidateQueries({ queryKey: ['platform', 'tenant', subscription?.tenantId] }),
      ])
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const item = detail.data
  const activePlans = (plans.data ?? []).filter((plan) => plan.isActive)
  const actionDescription =
    action?.type === 'activate'
      ? 'This manually activates the subscription. It is not a payment and does not confirm that money was collected.'
      : action?.type === 'change-plan'
        ? 'The plan will change immediately and the current period will restart. This is a manual operation; no payment will be processed.'
        : action?.type === 'extend'
          ? `The current period will be extended by ${action.days} days.`
          : action?.type === 'status' && action.status === 'Suspended'
            ? 'The tenant subscription will be suspended and its entitlements will no longer be active.'
            : 'The subscription will be reactivated. This action does not process a payment.'

  return (
    <>
      <Dialog
        open={subscription !== null}
        onOpenChange={(open) => {
          if (!open) onClose()
        }}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Subscription details</DialogTitle>
            <DialogDescription>
              {subscription?.tenantName ?? 'Tenant subscription'}
            </DialogDescription>
          </DialogHeader>

          {detail.isError ? (
            <ErrorState message={errorMessage(detail.error)} onRetry={() => void detail.refetch()} />
          ) : detail.isLoading || !item ? (
            <div className="space-y-3" aria-busy="true" aria-label="Loading subscription details">
              <Skeleton className="h-36 rounded-lg" />
              <Skeleton className="h-24 rounded-lg" />
            </div>
          ) : (
            <div className="space-y-5">
              <DetailList>
                <DetailRow label="Tenant">
                  <Link className="text-primary hover:underline" to={`/platform/tenants/${item.tenantId}`} onClick={onClose}>
                    {item.tenantName}
                    <ExternalLink className="ml-1 inline size-3" aria-hidden />
                  </Link>
                </DetailRow>
                <DetailRow label="Plan">{item.planName} ({item.planCode})</DetailRow>
                <DetailRow label="Status">
                  <StatusBadge tone={item.status === 'Active' ? 'success' : item.status === 'Suspended' ? 'danger' : 'warning'}>
                    {item.status}
                  </StatusBadge>
                </DetailRow>
                <DetailRow label="Activation source">{item.activationSource}</DetailRow>
                <DetailRow label="Payment confirmed">{item.paymentConfirmed ? 'Yes' : 'No'}</DetailRow>
                <DetailRow label="Start date">{formatDate(item.startDate)}</DetailRow>
                <DetailRow label="Current period">
                  {formatDate(item.currentPeriodStart)} – {item.currentPeriodEnd ? formatDate(item.currentPeriodEnd) : 'No end date'}
                </DetailRow>
              </DetailList>

              <div className="rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm text-warning-foreground">
                Platform activation and plan changes are manual/simulated. They do not represent a completed payment.
              </div>

              {plans.isError ? (
                <ErrorState message={errorMessage(plans.error)} onRetry={() => void plans.refetch()} />
              ) : (
                <div className="grid gap-3 md:grid-cols-2">
                  <label className="text-xs font-medium text-muted-foreground">
                    Change plan
                    <Select value={planCode} onValueChange={setPlanCode} disabled={plans.isLoading}>
                      <SelectTrigger className="mt-1 w-full" aria-label="New subscription plan">
                        <SelectValue placeholder={plans.isLoading ? 'Loading plans…' : 'Choose a plan'} />
                      </SelectTrigger>
                      <SelectContent>
                        {activePlans.map((plan) => (
                          <SelectItem key={plan.code} value={plan.code} disabled={plan.code === item.planCode}>
                            {plan.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </label>
                  <div className="flex items-end">
                    <Button
                      variant="outline"
                      className="w-full"
                      disabled={!planCode || planCode === item.planCode || mutation.isPending}
                      onClick={() => setAction({ type: 'change-plan', planCode })}
                    >
                      Change plan
                    </Button>
                  </div>
                  <label className="text-xs font-medium text-muted-foreground">
                    Extend period (days)
                    <Input
                      type="number"
                      min={1}
                      max={365}
                      step={1}
                      value={days}
                      onChange={(event) => setDays(event.target.value)}
                      className="mt-1"
                      aria-label="Subscription extension in days"
                    />
                  </label>
                  <div className="flex items-end">
                    <Button
                      variant="outline"
                      className="w-full"
                      disabled={
                        !Number.isInteger(Number(days))
                        || Number(days) < 1
                        || Number(days) > 365
                        || mutation.isPending
                      }
                      onClick={() => setAction({ type: 'extend', days: Number(days) })}
                    >
                      Extend period
                    </Button>
                  </div>
                </div>
              )}

              <DialogFooter className="flex-wrap">
                {item.status === 'Active' ? (
                  <Button
                    variant="destructive"
                    disabled={mutation.isPending}
                    onClick={() => setAction({ type: 'status', status: 'Suspended' })}
                  >
                    Suspend subscription
                  </Button>
                ) : item.status === 'Suspended' ? (
                  <Button
                    variant="outline"
                    disabled={mutation.isPending}
                    onClick={() => setAction({ type: 'status', status: 'Active' })}
                  >
                    Reactivate subscription
                  </Button>
                ) : (
                  <Button
                    disabled={mutation.isPending}
                    onClick={() => setAction({ type: 'activate' })}
                  >
                    Manually activate
                  </Button>
                )}
              </DialogFooter>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={action !== null}
        onOpenChange={(open) => {
          if (!open) setAction(null)
        }}
        title={
          action?.type === 'activate'
            ? 'Manually activate this subscription?'
            : action?.type === 'change-plan'
              ? 'Change subscription plan?'
              : action?.type === 'extend'
                ? 'Extend subscription period?'
                : action?.type === 'status' && action.status === 'Suspended'
                  ? 'Suspend this subscription?'
                  : 'Reactivate this subscription?'
        }
        description={actionDescription}
        confirmLabel={action?.type === 'activate' ? 'Activate manually' : 'Confirm'}
        destructive={action?.type === 'status' && action.status === 'Suspended'}
        pending={mutation.isPending}
        onConfirm={() => {
          if (action) mutation.mutate(action)
        }}
      />
    </>
  )
}

export function PlatformSubscriptionsPage() {
  const [selectedSubscription, setSelectedSubscription] = useState<PlatformSubscriptionDto | null>(null)
  const table = useTableParams(25)
  const status = table.get('status')
  const subscriptions = useQuery({
    queryKey: ['platform', 'subscriptions', table.page, table.pageSize, status],
    queryFn: () => getPlatformSubscriptions({ page: table.page, pageSize: table.pageSize, status }),
  })

  const columns: DataTableColumn<PlatformSubscriptionDto>[] = [
    {
      id: 'tenant',
      header: 'Tenant',
      cell: (item) => (
        <Link
          to={`/platform/tenants/${item.tenantId}`}
          className="text-sm font-medium text-foreground hover:text-primary"
        >
          {item.tenantName}
        </Link>
      ),
    },
    { id: 'plan', header: 'Plan', cell: (item) => <span className="text-sm">{item.planName}</span> },
    {
      id: 'status',
      header: 'Status',
      cell: (item) => (
        <StatusBadge tone={item.status === 'Active' ? 'success' : item.status === 'Suspended' ? 'danger' : 'warning'}>
          {item.status}
        </StatusBadge>
      ),
    },
    { id: 'start', header: 'Start date', cell: (item) => <span className="text-xs">{formatDate(item.startDate)}</span> },
    {
      id: 'period',
      header: 'Current period',
      className: 'hidden lg:table-cell',
      cell: (item) => (
        <span className="text-xs text-muted-foreground">
          {formatDate(item.currentPeriodStart)} – {item.currentPeriodEnd ? formatDate(item.currentPeriodEnd) : 'No end'}
        </span>
      ),
    },
    {
      id: 'actions',
      header: '',
      className: 'text-right',
      cell: (item) => (
        <Button size="sm" variant="outline" onClick={() => setSelectedSubscription(item)}>
          View
        </Button>
      ),
    },
  ]

  return (
    <div>
      <PageHeader
        title="Subscriptions"
        description="Review subscription periods and make manual operational changes."
        actions={<Badge variant="outline">No payment processing</Badge>}
      />

      <FilterBar>
        <Select value={status || 'All'} onValueChange={(value) => table.update({ status: value === 'All' ? '' : value })}>
          <SelectTrigger className="w-full sm:w-48" aria-label="Filter subscription status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All statuses</SelectItem>
            <SelectItem value="Trialing">Trialing</SelectItem>
            <SelectItem value="Active">Active</SelectItem>
            <SelectItem value="PastDue">Past due</SelectItem>
            <SelectItem value="Suspended">Suspended</SelectItem>
            <SelectItem value="Canceled">Canceled</SelectItem>
            <SelectItem value="Expired">Expired</SelectItem>
          </SelectContent>
        </Select>
      </FilterBar>

      {subscriptions.isError ? (
        <ErrorState message={errorMessage(subscriptions.error)} onRetry={() => void subscriptions.refetch()} />
      ) : subscriptions.isLoading || !subscriptions.data ? (
        <div className="space-y-3" aria-busy="true" aria-label="Loading subscriptions">
          <Skeleton className="h-12 rounded-lg" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={subscriptions.data.items}
            getRowKey={(item) => item.id}
            caption="Platform subscriptions"
            empty={
              <EmptyState
                icon={<CreditCard className="size-4" aria-hidden />}
                title="No subscriptions found"
                description="Subscriptions matching this status will appear here."
              />
            }
          />
          {subscriptions.data.totalCount > 0 ? (
            <ListPagination
              page={subscriptions.data.page}
              pageSize={subscriptions.data.pageSize}
              totalCount={subscriptions.data.totalCount}
              totalPages={subscriptions.data.totalPages}
              onPageChange={table.setPage}
              onPageSizeChange={table.setPageSize}
              className="px-1"
            />
          ) : null}
        </>
      )}

      <SubscriptionDetailsDialog
        subscription={selectedSubscription}
        onClose={() => setSelectedSubscription(null)}
      />
    </div>
  )
}
