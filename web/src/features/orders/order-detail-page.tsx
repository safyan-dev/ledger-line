import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Ban, ExternalLink, Receipt } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Progress } from '@/components/ui/progress'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { CopyableId } from '@/components/data/copy-button'
import { DetailList, DetailRow } from '@/components/data/detail-list'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { Money } from '@/components/data/money'
import { StatusBadge } from '@/components/data/status-badge'
import { Timeline } from '@/components/data/timeline'
import { PageHeader } from '@/components/layout/page-header'
import { cancelOrder, getOrder } from '@/features/orders/api'
import { getPayments } from '@/features/payments/api'
import { getReconciliationRecords } from '@/features/reconciliation/api'
import { getAuditLogs } from '@/features/audit/api'
import { errorMessage, errorStateProps } from '@/lib/api-client'
import { formatDateTime, formatRelative } from '@/lib/format'
import { orderStatusMeta, paymentStatusMeta, reconciliationOutcomeMeta, strategyLabel } from '@/lib/status'
import type { PaymentListItemDto, ReconciliationRecordDto } from '@/types/api'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'
import { invalidateRelated } from '@/lib/invalidate'

const paymentColumns: DataTableColumn<PaymentListItemDto>[] = [
  {
    id: 'payment',
    header: 'Payment',
    cell: (payment) => (
      <div>
        <p className="font-mono text-xs font-medium text-foreground">{payment.providerPaymentId}</p>
        <p className="mt-0.5 text-[11px] text-muted-foreground">{payment.provider}</p>
      </div>
    ),
  },
  {
    id: 'amount',
    header: 'Amount',
    className: 'text-right',
    cell: (payment) => <Money amount={payment.amount} currency={payment.currency} />,
  },
  {
    id: 'status',
    header: 'Status',
    cell: (payment) => (
      <StatusBadge tone={paymentStatusMeta(payment.status).tone}>{paymentStatusMeta(payment.status).label}</StatusBadge>
    ),
  },
  {
    id: 'received',
    header: 'Received',
    className: 'text-right whitespace-nowrap',
    cell: (payment) => <span className="text-xs text-muted-foreground">{formatDateTime(payment.receivedAt)}</span>,
  },
]

const reconciliationColumns: DataTableColumn<ReconciliationRecordDto>[] = [
  {
    id: 'outcome',
    header: 'Outcome',
    cell: (record) => (
      <StatusBadge tone={reconciliationOutcomeMeta(record.outcome).tone}>
        {reconciliationOutcomeMeta(record.outcome).label}
      </StatusBadge>
    ),
  },
  {
    id: 'strategy',
    header: 'Strategy',
    cell: (record) => <span className="text-xs text-muted-foreground">{strategyLabel(record.strategy)}</span>,
  },
  {
    id: 'expected',
    header: 'Expected',
    className: 'text-right',
    cell: (record) =>
      record.expectedAmount === null ? (
        <span className="text-xs text-muted-foreground">—</span>
      ) : (
        <Money amount={record.expectedAmount} currency={record.currency} />
      ),
  },
  {
    id: 'difference',
    header: 'Difference',
    className: 'text-right',
    cell: (record) => (
      <Money
        amount={record.difference}
        currency={record.currency}
        className={record.difference < 0 ? 'text-warning-foreground' : undefined}
      />
    ),
  },
  {
    id: 'matchedBy',
    header: 'Matched by',
    cell: (record) => <span className="text-xs text-muted-foreground">{record.matchedBy}</span>,
  },
  {
    id: 'when',
    header: 'When',
    className: 'text-right whitespace-nowrap',
    cell: (record) => <span className="text-xs text-muted-foreground">{formatRelative(record.createdAt)}</span>,
  },
]

export function OrderDetailPage() {
  const tenantId = useTenantKey()
  const { orderId = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [cancelOpen, setCancelOpen] = useState(false)
  const [reason, setReason] = useState('')

  const orderQuery = useQuery({ queryKey: qk.order(tenantId, orderId), queryFn: () => getOrder(orderId), enabled: Boolean(orderId) })
  const paymentsQuery = useQuery({
    queryKey: qk.paymentsForOrder(tenantId, orderId),
    queryFn: () => getPayments({ page: 1, pageSize: 100, orderId }),
    enabled: Boolean(orderId),
  })
  const reconciliationQuery = useQuery({
    queryKey: qk.reconciliationForOrder(tenantId, orderId),
    queryFn: () => getReconciliationRecords({ page: 1, pageSize: 100, orderId }),
    enabled: Boolean(orderId),
  })
  const auditQuery = useQuery({
    queryKey: qk.auditFor(tenantId, 'order', orderId),
    queryFn: () => getAuditLogs({ page: 1, pageSize: 10, entityType: 'Order', entityId: orderId }),
    enabled: Boolean(orderId),
  })

  const cancelMutation = useMutation({
    mutationFn: () => cancelOrder(orderId, reason),
    onSuccess: () => {
      toast.success('Order cancelled')
      setCancelOpen(false)
      setReason('')
      invalidateRelated(queryClient, 'order')
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  if (orderQuery.isError) {
    return (
      <>
        <PageHeader title="Order" description="Order detail and allocation trail." />
        <ErrorState {...errorStateProps(orderQuery.error)} onRetry={() => void orderQuery.refetch()} />
      </>
    )
  }

  if (orderQuery.isLoading || !orderQuery.data) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-56" />
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  const order = orderQuery.data
  const paidRatio = order.expectedAmount === 0 ? 0 : Math.min(100, (order.paidAmount / order.expectedAmount) * 100)
  const canCancel = order.status !== 'Cancelled' && order.paidAmount === 0

  return (
    <>
      <Button variant="ghost" size="sm" className="mb-3 -ml-2 text-muted-foreground" onClick={() => navigate('/orders')}>
        <ArrowLeft className="size-4" aria-hidden />
        Orders
      </Button>

      <PageHeader
        title={order.orderNumber}
        description={order.description ?? `${order.customerName} · ${order.customerReference ?? 'no customer reference'}`}
        actions={
          <Button variant="outline" onClick={() => setCancelOpen(true)} disabled={!canCancel}>
            <Ban className="size-4" aria-hidden />
            Cancel order
          </Button>
        }
      />

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_340px]">
        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader className="flex-row items-start justify-between space-y-0 pb-2">
            <div>
              <CardTitle className="text-base">Allocation</CardTitle>
              <CardDescription>How much of the expected amount has been matched.</CardDescription>
            </div>
            <StatusBadge tone={orderStatusMeta(order.status).tone}>{orderStatusMeta(order.status).label}</StatusBadge>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-3">
              <div>
                <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Expected</p>
                <p className="mt-1 text-lg font-semibold tabular-nums">
                  <Money amount={order.expectedAmount} currency={order.currency} />
                </p>
              </div>
              <div>
                <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Paid</p>
                <p className="mt-1 text-lg font-semibold tabular-nums">
                  <Money amount={order.paidAmount} currency={order.currency} />
                </p>
              </div>
              <div>
                <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Remaining</p>
                <p className="mt-1 text-lg font-semibold tabular-nums">
                  <Money amount={order.remainingAmount} currency={order.currency} />
                </p>
              </div>
            </div>

            <div className="space-y-1.5">
              <Progress value={paidRatio} className="h-2" />
              <div className="flex justify-between text-[11px] text-muted-foreground">
                <span>{paidRatio.toFixed(0)}% allocated</span>
                <span>Updated {formatRelative(order.updatedAt)}</span>
              </div>
            </div>

            {!canCancel && order.status !== 'Cancelled' ? (
              <p className="rounded-lg border border-dashed bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
                Cancelling is blocked while payments are allocated. Reverse the payments first.
              </p>
            ) : null}

            <DetailList className="border-t pt-2">
              <DetailRow label="Order number">
                <CopyableId value={order.orderNumber} label="order number" />
              </DetailRow>
              <DetailRow label="Customer">{order.customerName}</DetailRow>
              <DetailRow label="Customer reference">
                {order.customerReference ? (
                  <CopyableId value={order.customerReference} label="customer reference" />
                ) : (
                  '—'
                )}
              </DetailRow>
              <DetailRow label="Currency">{order.currency}</DetailRow>
              <DetailRow label="Created">{formatDateTime(order.createdAt)}</DetailRow>
            </DetailList>
          </CardContent>
        </Card>

        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Audit trail</CardTitle>
            <CardDescription>Latest actions touching this order.</CardDescription>
          </CardHeader>
          <CardContent>
            {auditQuery.data?.items.length ? (
              <Timeline
                items={auditQuery.data.items.map((item) => ({
                  id: item.id,
                  title: item.action.replace(/\./g, ' · '),
                  description: item.summary,
                  timestamp: formatRelative(item.occurredAt),
                  tone: item.action.includes('cancel') ? 'warning' : 'info',
                }))}
              />
            ) : (
              <p className="text-xs text-muted-foreground">No audit entries yet.</p>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="mt-6">
        <div className="mb-2 flex items-center justify-between">
          <h2 className="text-sm font-semibold tracking-tight">Payment attempts</h2>
          <Link to={`/payments?orderId=${order.id}`} className="text-xs font-medium text-primary hover:underline">
            Open in payments
          </Link>
        </div>
        <DataTable
          columns={paymentColumns}
          rows={paymentsQuery.data?.items ?? []}
          getRowKey={(payment) => payment.id}
          caption="Payments allocated to this order"
          isLoading={paymentsQuery.isLoading}
          onRowClick={(payment) => navigate(`/payments/${payment.id}`)}
          empty={
            <EmptyState
              icon={<Receipt className="size-4" aria-hidden />}
              title="No payments yet"
              description="Record a payment or replay a provider webhook to create one."
              action={
                <Button variant="outline" size="sm" asChild>
                  <Link to="/payments">
                    Go to payments
                    <ExternalLink className="size-3.5" aria-hidden />
                  </Link>
                </Button>
              }
            />
          }
        />
      </div>

      <div className="mt-6">
        <div className="mb-2 flex items-center justify-between">
          <h2 className="text-sm font-semibold tracking-tight">Reconciliation trail</h2>
          <Link to={`/reconciliation?orderId=${order.id}`} className="text-xs font-medium text-primary hover:underline">
            Open in reconciliation
          </Link>
        </div>
        <DataTable
          columns={reconciliationColumns}
          rows={reconciliationQuery.data?.items ?? []}
          getRowKey={(record) => record.id}
          caption="Reconciliation attempts for this order"
          isLoading={reconciliationQuery.isLoading}
          empty={<EmptyState title="No reconciliation attempts" description="Attempts appear when a payment is recorded." />}
        />
      </div>

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Cancel {order.orderNumber}</DialogTitle>
            <DialogDescription>
              Cancelling is permanent and is recorded in the audit log. It is only allowed before any payment is allocated.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="cancel-reason">Reason</Label>
            <Textarea
              id="cancel-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Customer withdrew the purchase order."
              maxLength={400}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)} disabled={cancelMutation.isPending}>
              Keep order
            </Button>
            <Button
              variant="destructive"
              onClick={() => cancelMutation.mutate()}
              disabled={!reason.trim() || cancelMutation.isPending}
            >
              <Ban className="size-4" aria-hidden />
              Cancel order
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
