import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ExternalLink, Link2, Loader2, RefreshCw, Undo2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { ErrorState } from '@/components/data/error-state'
import { DetailList, DetailRow } from '@/components/data/detail-list'
import { Money } from '@/components/data/money'
import { RefundDialog } from '@/components/data/refund-dialog'
import { StatusBadge } from '@/components/data/status-badge'
import { Timeline } from '@/components/data/timeline'
import { ManualMatchDialog } from '@/components/data/manual-match-dialog'
import { getPayment, getRefunds, rerunReconciliation } from '@/features/payments/api'
import { getReconciliationForPayment } from '@/features/reconciliation/api'
import { errorMessage } from '@/lib/api-client'
import { formatDateTime, formatRelative } from '@/lib/format'
import { paymentStatusMeta, reconciliationOutcomeMeta, strategyLabel } from '@/lib/status'

export function PaymentDetailSheet({
  paymentId,
  onOpenChange,
}: {
  paymentId: string | null
  onOpenChange: (open: boolean) => void
}) {
  const [matchOpen, setMatchOpen] = useState(false)
  const [refundOpen, setRefundOpen] = useState(false)
  const queryClient = useQueryClient()

  const paymentQuery = useQuery({
    queryKey: ['payments', paymentId],
    queryFn: () => getPayment(paymentId!),
    enabled: Boolean(paymentId),
  })

  const refundsQuery = useQuery({
    queryKey: ['refunds', paymentId],
    queryFn: () => getRefunds(paymentId!),
    enabled: Boolean(paymentId),
  })

  const reconciliationQuery = useQuery({
    queryKey: ['reconciliation', 'payment', paymentId],
    queryFn: () => getReconciliationForPayment(paymentId!),
    enabled: Boolean(paymentId),
  })

  const rerun = useMutation({
    mutationFn: () => rerunReconciliation(paymentId!),
    onSuccess: () => {
      toast.success('Matching re-run')
      void queryClient.invalidateQueries({ queryKey: ['payments'] })
      void queryClient.invalidateQueries({ queryKey: ['reconciliation'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const payment = paymentQuery.data
  const isMatched = payment?.status === 'Matched'
  const canRefund = Boolean(payment && payment.status !== 'Failed' && payment.refundableAmount > 0)

  return (
    <>
      <Sheet open={Boolean(paymentId)} onOpenChange={onOpenChange}>
        <SheetContent side="right" className="w-full overflow-y-auto p-0 sm:max-w-md">
          <SheetHeader className="border-b px-6 py-4">
            <SheetTitle className="text-base">Payment detail</SheetTitle>
            <SheetDescription>
              {payment ? `${payment.provider} · ${payment.providerPaymentId}` : 'Loading payment…'}
            </SheetDescription>
          </SheetHeader>

          <div className="space-y-6 px-6 py-5">
            {paymentQuery.isError ? (
              <ErrorState message={paymentQuery.error.message} onRetry={() => void paymentQuery.refetch()} />
            ) : paymentQuery.isLoading || !payment ? (
              <div className="space-y-3">
                <Skeleton className="h-6 w-2/3" />
                <Skeleton className="h-24 w-full" />
                <Skeleton className="h-40 w-full" />
              </div>
            ) : (
              <>
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-2xl font-semibold tabular-nums">
                      <Money amount={payment.amount} currency={payment.currency} />
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(payment.receivedAt)}</p>
                  </div>
                  <StatusBadge tone={paymentStatusMeta(payment.status).tone}>
                    {paymentStatusMeta(payment.status).label}
                  </StatusBadge>
                </div>

                <DetailList>
                  <DetailRow label="Provider">{payment.provider}</DetailRow>
                  <DetailRow label="Provider payment id">
                    <span className="font-mono text-xs">{payment.providerPaymentId}</span>
                  </DetailRow>
                  <DetailRow label="Customer">{payment.customerName ?? '—'}</DetailRow>
                  <DetailRow label="Customer reference">{payment.customerReference ?? '—'}</DetailRow>
                  <DetailRow label="Reference">{payment.reference ?? '—'}</DetailRow>
                  <DetailRow label="Order">
                    {payment.orderId ? (
                      <Link
                        to={`/orders/${payment.orderId}`}
                        onClick={() => onOpenChange(false)}
                        className="inline-flex items-center gap-1 text-primary hover:underline"
                      >
                        {payment.orderNumber ?? payment.orderId}
                        <ExternalLink className="size-3" aria-hidden />
                      </Link>
                    ) : (
                      'Not linked'
                    )}
                  </DetailRow>
                  {payment.failureReason ? <DetailRow label="Hold reason">{payment.failureReason}</DetailRow> : null}
                  {payment.refundedAmount > 0 ? (
                    <DetailRow label="Refunded">
                      <Money amount={payment.refundedAmount} currency={payment.currency} />
                    </DetailRow>
                  ) : null}
                  <DetailRow label="Refundable">
                    <Money amount={payment.refundableAmount} currency={payment.currency} />
                  </DetailRow>
                  <DetailRow label="Last updated">{formatRelative(payment.updatedAt)}</DetailRow>
                </DetailList>

                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" size="sm" onClick={() => setRefundOpen(true)} disabled={!canRefund}>
                    <Undo2 className="size-3.5" aria-hidden />
                    Refund
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => rerun.mutate()}
                    disabled={isMatched || rerun.isPending}
                  >
                    {rerun.isPending ? (
                      <Loader2 className="size-3.5 animate-spin" aria-hidden />
                    ) : (
                      <RefreshCw className="size-3.5" aria-hidden />
                    )}
                    Re-run matching
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => setMatchOpen(true)} disabled={isMatched}>
                    <Link2 className="size-3.5" aria-hidden />
                    Link to order
                  </Button>
                </div>

                <Separator />

                <div>
                  <h3 className="mb-3 text-sm font-semibold">Refunds</h3>
                  {refundsQuery.data?.length ? (
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
                  ) : (
                    <p className="text-xs text-muted-foreground">No refunds recorded for this payment.</p>
                  )}
                </div>

                <Separator />

                <div>
                  <h3 className="mb-3 text-sm font-semibold">Reconciliation attempts</h3>
                  {reconciliationQuery.data?.length ? (
                    <Timeline
                      items={reconciliationQuery.data.map((record) => ({
                        id: record.id,
                        title: `${reconciliationOutcomeMeta(record.outcome).label} · ${strategyLabel(record.strategy)}`,
                        description: (
                          <>
                            {record.reason ?? 'No reason recorded.'}
                            {record.notes ? ` ${record.notes}` : ''}
                          </>
                        ),
                        timestamp: formatRelative(record.createdAt),
                        tone:
                          record.outcome === 'Unmatched' || record.outcome === 'Ambiguous'
                            ? 'warning'
                            : record.outcome === 'Overpaid'
                              ? 'info'
                              : 'success',
                      }))}
                    />
                  ) : (
                    <p className="text-xs text-muted-foreground">No reconciliation attempts recorded yet.</p>
                  )}
                </div>
              </>
            )}
          </div>
        </SheetContent>
      </Sheet>

      <ManualMatchDialog
        open={matchOpen}
        onOpenChange={setMatchOpen}
        paymentId={paymentId ?? undefined}
        paymentLabel={payment ? `${payment.providerPaymentId} · ${payment.customerName ?? 'Unknown customer'}` : undefined}
      />

      <RefundDialog
        open={refundOpen}
        onOpenChange={setRefundOpen}
        paymentId={paymentId ?? undefined}
        maxAmount={payment?.refundableAmount ?? 0}
        currency={payment?.currency}
        paymentLabel={payment ? `${payment.providerPaymentId} · ${payment.customerName ?? 'Unknown customer'}` : undefined}
      />
    </>
  )
}
