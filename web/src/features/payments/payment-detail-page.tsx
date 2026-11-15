import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft,
  ExternalLink,
  Link2,
  Loader2,
  RefreshCw,
  ScrollText,
  Undo2,
  Webhook,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { Skeleton } from '@/components/ui/skeleton'
import { AmountComparison } from '@/components/data/amount-comparison'
import { CopyableId } from '@/components/data/copy-button'
import { DetailList, DetailRow } from '@/components/data/detail-list'
import { DetailSection } from '@/components/data/detail-section'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { Money } from '@/components/data/money'
import { RefundDialog } from '@/components/data/refund-dialog'
import { StatusBadge } from '@/components/data/status-badge'
import { Timeline } from '@/components/data/timeline'
import { ManualMatchDialog } from '@/components/data/manual-match-dialog'
import { PageHeader } from '@/components/layout/page-header'
import { getOrder } from '@/features/orders/api'
import { getAuditLogs } from '@/features/audit/api'
import { getPayment, getRefunds, rerunReconciliation } from '@/features/payments/api'
import { getReconciliationForPayment } from '@/features/reconciliation/api'
import { getWebhookEvents } from '@/features/webhooks/api'
import { errorMessage } from '@/lib/api-client'
import { formatDateTime, formatRelative } from '@/lib/format'
import {
  actionLabel,
  orderStatusMeta,
  paymentStatusMeta,
  reconciliationOutcomeMeta,
  strategyLabel,
  webhookStatusMeta,
} from '@/lib/status'

export function PaymentDetailPage() {
  const { paymentId = '' } = useParams()
  const queryClient = useQueryClient()
  const [activeAction, setActiveAction] = useState<'match' | 'refund' | null>(null)

  const paymentQuery = useQuery({
    queryKey: ['payments', paymentId],
    queryFn: () => getPayment(paymentId),
    enabled: Boolean(paymentId),
  })
  const payment = paymentQuery.data

  const orderQuery = useQuery({
    queryKey: ['orders', payment?.orderId],
    queryFn: () => getOrder(payment!.orderId!),
    enabled: Boolean(payment?.orderId),
  })
  const refundsQuery = useQuery({
    queryKey: ['refunds', paymentId],
    queryFn: () => getRefunds(paymentId),
    enabled: Boolean(paymentId),
  })
  const reconciliationQuery = useQuery({
    queryKey: ['reconciliation', 'payment', paymentId],
    queryFn: () => getReconciliationForPayment(paymentId),
    enabled: Boolean(paymentId),
  })
  const webhooksQuery = useQuery({
    queryKey: ['webhooks', 'payment', paymentId],
    queryFn: () => getWebhookEvents({ page: 1, pageSize: 50, paymentId }),
    enabled: Boolean(paymentId),
  })
  const auditQuery = useQuery({
    queryKey: ['audit', 'payment', paymentId],
    queryFn: () => getAuditLogs({ page: 1, pageSize: 50, entityType: 'Payment', entityId: paymentId }),
    enabled: Boolean(paymentId),
  })

  const rerun = useMutation({
    mutationFn: () => rerunReconciliation(paymentId),
    onSuccess: () => {
      toast.success('Matching re-run')
      void queryClient.invalidateQueries({ queryKey: ['payments'] })
      void queryClient.invalidateQueries({ queryKey: ['reconciliation'] })
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  if (paymentQuery.isError) {
    return (
      <>
        <BackLink />
        <PageHeader title="Payment detail" description="Transaction, reconciliation and processing history." />
        <ErrorState message={paymentQuery.error.message} onRetry={() => void paymentQuery.refetch()} />
      </>
    )
  }

  if (paymentQuery.isLoading || !payment) {
    return (
      <>
        <BackLink />
        <PageHeader title="Payment detail" description="Transaction, reconciliation and processing history." />
        <div className="space-y-4">
          <Skeleton className="h-32 w-full rounded-xl" />
          <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
            <Skeleton className="h-96 w-full rounded-xl" />
            <Skeleton className="h-96 w-full rounded-xl" />
          </div>
        </div>
      </>
    )
  }

  const order = orderQuery.data
  const records = reconciliationQuery.data ?? []
  const latestRecord = records[0]
  const webhookEvents = webhooksQuery.data?.items ?? []
  const auditItems = auditQuery.data?.items ?? []
  const isMatched = payment.status === 'Matched'
  const canRefund = payment.status !== 'Failed' && payment.refundableAmount > 0
  const paidRatio = order && order.expectedAmount > 0 ? Math.min(100, (order.paidAmount / order.expectedAmount) * 100) : 0

  return (
    <>
      <BackLink />

      <PageHeader
        title="Payment detail"
        description={`${payment.provider} · ${payment.providerPaymentId}`}
        actions={
          <>
            <Button variant="outline" onClick={() => setActiveAction('refund')} disabled={!canRefund}>
              <Undo2 className="size-4" aria-hidden />
              Refund
            </Button>
            <Button variant="outline" onClick={() => rerun.mutate()} disabled={isMatched || rerun.isPending}>
              {rerun.isPending ? (
                <Loader2 className="size-4 animate-spin" aria-hidden />
              ) : (
                <RefreshCw className="size-4" aria-hidden />
              )}
              Re-run matching
            </Button>
            <Button variant="outline" onClick={() => setActiveAction('match')} disabled={isMatched}>
              <Link2 className="size-4" aria-hidden />
              Link to order
            </Button>
          </>
        }
      />

      <Card className="mb-4 border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <CardContent className="flex flex-wrap items-center justify-between gap-5 p-5">
          <div>
            <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Payment amount</p>
            <p className="mt-1 text-3xl font-semibold tracking-tight tabular-nums text-foreground">
              <Money amount={payment.amount} currency={payment.currency} />
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              Received {formatDateTime(payment.receivedAt)} · {payment.currency}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-5">
            <div>
              <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Status</p>
              <div className="mt-1.5">
                <StatusBadge tone={paymentStatusMeta(payment.status).tone}>
                  {paymentStatusMeta(payment.status).label}
                </StatusBadge>
              </div>
            </div>
            <div>
              <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Provider</p>
              <p className="mt-1.5 text-sm font-medium capitalize text-foreground">{payment.provider}</p>
            </div>
            <div>
              <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Refundable</p>
              <p className="mt-1.5 text-sm font-medium tabular-nums text-foreground">
                <Money amount={payment.refundableAmount} currency={payment.currency} />
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
        <div className="min-w-0 space-y-4">
          <DetailSection title="Transaction" description="Provider identifiers and payment metadata.">
            <DetailList>
              <DetailRow label="Payment id">
                <CopyableId value={payment.id} label="payment id" />
              </DetailRow>
              <DetailRow label="Provider payment id">
                <CopyableId value={payment.providerPaymentId} label="provider payment id" />
              </DetailRow>
              <DetailRow label="Provider">{payment.provider}</DetailRow>
              {payment.providerOrderId ? (
                <DetailRow label="Provider order id">
                  <CopyableId value={payment.providerOrderId} label="provider order id" />
                </DetailRow>
              ) : null}
              <DetailRow label="Reference">
                {payment.reference ? <CopyableId value={payment.reference} label="reference" /> : '—'}
              </DetailRow>
              <DetailRow label="Customer">{payment.customerName ?? '—'}</DetailRow>
              <DetailRow label="Customer reference">
                {payment.customerReference ? (
                  <CopyableId value={payment.customerReference} label="customer reference" />
                ) : (
                  '—'
                )}
              </DetailRow>
              <DetailRow label="Received">{formatDateTime(payment.receivedAt)}</DetailRow>
              <DetailRow label="Created">{formatDateTime(payment.createdAt)}</DetailRow>
              <DetailRow label="Last updated">{formatDateTime(payment.updatedAt)}</DetailRow>
              {payment.failureReason ? <DetailRow label="Failure reason">{payment.failureReason}</DetailRow> : null}
            </DetailList>
          </DetailSection>

          <DetailSection
            title="Amount comparison"
            description="Internal order expectation against the provider transaction."
          >
            <AmountComparison
              expectedAmount={latestRecord?.expectedAmount ?? order?.expectedAmount}
              expectedCurrency={latestRecord?.currency ?? order?.currency}
              actualAmount={payment.amount}
              actualCurrency={payment.currency}
              outcome={latestRecord?.outcome}
              reason={latestRecord?.reason}
            />
          </DetailSection>

          <DetailSection
            title="Reconciliation history"
            description="Each attempt, the rule that fired, and the resulting difference."
          >
            {reconciliationQuery.isLoading ? (
              <div className="space-y-3">
                <Skeleton className="h-16 w-full" />
                <Skeleton className="h-16 w-full" />
              </div>
            ) : reconciliationQuery.isError ? (
              <p className="text-xs text-muted-foreground">Reconciliation history could not be loaded.</p>
            ) : records.length === 0 ? (
              <EmptyState
                title="No reconciliation attempts"
                description="An attempt is created automatically when a payment is recorded."
              />
            ) : (
              <Timeline
                items={records.map((record) => ({
                  id: record.id,
                  title: (
                    <span className="flex flex-wrap items-center gap-2">
                      <StatusBadge tone={reconciliationOutcomeMeta(record.outcome).tone}>
                        {reconciliationOutcomeMeta(record.outcome).label}
                      </StatusBadge>
                      <span className="text-xs font-normal text-muted-foreground">
                        {strategyLabel(record.strategy)}
                      </span>
                    </span>
                  ),
                  description: (
                    <>
                      <span className="block">{record.reason ?? 'No reason recorded.'}</span>
                      <span className="mt-1 block tabular-nums text-muted-foreground">
                        Actual <Money amount={record.actualAmount} currency={record.currency} />
                        {record.expectedAmount !== null ? (
                          <>
                            {' '}
                            · expected <Money amount={record.expectedAmount} currency={record.currency} /> · difference{' '}
                            <Money amount={record.difference} currency={record.currency} />
                          </>
                        ) : null}
                      </span>
                    </>
                  ),
                  timestamp: formatRelative(record.createdAt),
                  tone:
                    record.outcome === 'Matched' || record.outcome === 'ManuallyMatched'
                      ? 'success'
                      : record.outcome === 'PartiallyMatched'
                        ? 'info'
                        : 'warning',
                }))}
              />
            )}
          </DetailSection>

          <DetailSection
            title="Webhook processing"
            description="Provider events linked to this payment, with receipt and processing times."
          >
            {webhooksQuery.isLoading ? (
              <div className="space-y-3">
                <Skeleton className="h-14 w-full" />
                <Skeleton className="h-14 w-full" />
              </div>
            ) : webhooksQuery.isError ? (
              <p className="text-xs text-muted-foreground">Webhook history could not be loaded.</p>
            ) : webhookEvents.length === 0 ? (
              <EmptyState
                icon={<Webhook className="size-4" aria-hidden />}
                title="No webhook events linked"
                description="Manual payments do not have a provider webhook. Simulated provider payments do."
              />
            ) : (
              <ul className="divide-y divide-border/70 rounded-lg border">
                {webhookEvents.map((event) => (
                  <li key={event.id} className="space-y-2 px-3 py-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex min-w-0 flex-wrap items-center gap-2">
                        <StatusBadge tone={webhookStatusMeta(event.status).tone}>
                          {webhookStatusMeta(event.status).label}
                        </StatusBadge>
                        <CopyableId value={event.providerEventId} label="webhook event id" />
                      </div>
                      <Link
                        to={`/webhooks?search=${encodeURIComponent(event.providerEventId)}`}
                        className="inline-flex items-center gap-1 text-[11px] font-medium text-primary hover:underline"
                      >
                        Open in webhooks
                        <ExternalLink className="size-3" aria-hidden />
                      </Link>
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {event.eventType} · received {formatDateTime(event.receivedAt)}
                      {event.processedAt ? ` · processed ${formatDateTime(event.processedAt)}` : ''}
                    </p>
                    {event.failureReason ? <p className="text-xs text-destructive">{event.failureReason}</p> : null}
                    <p className="text-[11px] text-muted-foreground">
                      Attempts {event.attemptCount} · duplicates {event.duplicateCount} · conflicts {event.conflictCount}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </DetailSection>
        </div>

        <div className="min-w-0 space-y-4">
          <DetailSection title="Associated order" description="The order this payment is matched against.">
            {payment.orderId ? (
              orderQuery.isLoading ? (
                <Skeleton className="h-32 w-full" />
              ) : order ? (
                <div className="space-y-3">
                  <div className="flex items-center justify-between gap-2">
                    <Link to={`/orders/${order.id}`} className="font-medium text-primary hover:underline">
                      {order.orderNumber}
                    </Link>
                    <StatusBadge tone={orderStatusMeta(order.status).tone}>
                      {orderStatusMeta(order.status).label}
                    </StatusBadge>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {order.customerName} · {order.customerReference ?? 'No customer reference'}
                  </p>
                  <div className="grid grid-cols-3 gap-2 border-t pt-3 text-xs">
                    <div>
                      <p className="text-muted-foreground">Expected</p>
                      <p className="mt-1 font-medium tabular-nums">
                        <Money amount={order.expectedAmount} currency={order.currency} />
                      </p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Paid</p>
                      <p className="mt-1 font-medium tabular-nums">
                        <Money amount={order.paidAmount} currency={order.currency} />
                      </p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Remaining</p>
                      <p className="mt-1 font-medium tabular-nums">
                        <Money amount={order.remainingAmount} currency={order.currency} />
                      </p>
                    </div>
                  </div>
                  <div className="space-y-1">
                    <Progress value={paidRatio} className="h-1.5" />
                    <p className="text-[11px] text-muted-foreground">{paidRatio.toFixed(0)}% allocated</p>
                  </div>
                </div>
              ) : (
                <p className="text-xs text-muted-foreground">The linked order could not be loaded.</p>
              )
            ) : (
              <EmptyState
                title="Not linked to an order"
                description="Use Link to order to attach this payment after review."
                action={
                  <Button variant="outline" size="sm" onClick={() => setActiveAction('match')}>
                    <Link2 className="size-3.5" aria-hidden />
                    Link to order
                  </Button>
                }
              />
            )}
          </DetailSection>

          <DetailSection
            title="Activity timeline"
            description="Audit entries recorded for this payment."
            actions={<ScrollText className="size-4 text-muted-foreground" aria-hidden />}
          >
            {auditQuery.isLoading ? (
              <div className="space-y-3">
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
              </div>
            ) : auditQuery.isError ? (
              <p className="text-xs text-muted-foreground">Audit history could not be loaded.</p>
            ) : auditItems.length === 0 ? (
              <p className="text-xs text-muted-foreground">No audit entries recorded for this payment.</p>
            ) : (
              <Timeline
                items={auditItems.map((item) => ({
                  id: item.id,
                  title: actionLabel(item.action),
                  description: item.summary,
                  timestamp: formatRelative(item.occurredAt),
                  tone: item.action.includes('failed') || item.action.includes('unmatched') ? 'warning' : 'info',
                }))}
              />
            )}
          </DetailSection>

          {refundsQuery.data?.length ? (
            <DetailSection title="Refunds" description="Refund history recorded against this payment.">
              <ul className="divide-y divide-border/70 rounded-lg border">
                {refundsQuery.data.map((refund) => (
                  <li key={refund.id} className="px-3 py-2.5">
                    <div className="flex items-baseline justify-between gap-3">
                      <p className="text-sm font-medium tabular-nums">
                        <Money amount={refund.amount} currency={refund.currency} />
                      </p>
                      <p className="text-[11px] text-muted-foreground">{formatRelative(refund.createdAt)}</p>
                    </div>
                    <p className="mt-0.5 text-xs text-muted-foreground">
                      {refund.reason} · {refund.createdBy} · {refund.status}
                    </p>
                  </li>
                ))}
              </ul>
            </DetailSection>
          ) : null}
        </div>
      </div>

      <ManualMatchDialog
        open={activeAction === 'match'}
        onOpenChange={(open) => setActiveAction(open ? 'match' : null)}
        paymentId={payment.id}
        paymentLabel={`${payment.providerPaymentId} · ${payment.customerName ?? 'Unknown customer'}`}
      />

      <RefundDialog
        open={activeAction === 'refund'}
        onOpenChange={(open) => setActiveAction(open ? 'refund' : null)}
        paymentId={payment.id}
        maxAmount={payment.refundableAmount}
        currency={payment.currency}
        paymentLabel={`${payment.providerPaymentId} · ${payment.customerName ?? 'Unknown customer'}`}
      />
    </>
  )
}

function BackLink() {
  const navigate = useNavigate()
  return (
    <Button variant="ghost" size="sm" className="mb-3 -ml-2 text-muted-foreground" onClick={() => navigate('/payments')}>
      <ArrowLeft className="size-4" aria-hidden />
      Payments
    </Button>
  )
}
