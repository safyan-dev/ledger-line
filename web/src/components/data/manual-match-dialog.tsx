import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Link2, Search } from 'lucide-react'
import { toast } from 'sonner'
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
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { StatusBadge } from '@/components/data/status-badge'
import { getOrders } from '@/features/orders/api'
import { getPayments } from '@/features/payments/api'
import { getReconciliationForPayment, manualMatch } from '@/features/reconciliation/api'
import { errorMessage } from '@/lib/api-client'
import { formatMoney } from '@/lib/format'
import { orderStatusMeta } from '@/lib/status'
import { cn } from '@/lib/utils'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'
import { invalidateRelated } from '@/lib/invalidate'
import { useDebouncedValue } from '@/lib/use-debounced-value'

export function ManualMatchDialog({
  open,
  onOpenChange,
  paymentId,
  paymentLabel,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  paymentId?: string
  paymentLabel?: string
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Match a payment to an order</DialogTitle>
          <DialogDescription>
            The payment is allocated to the order, the order status updates, and the action is written to the audit log.
          </DialogDescription>
        </DialogHeader>

        {/* Mounted only while open, so selection state starts empty each time
            without an effect to reset it. */}
        <ManualMatchForm
          paymentId={paymentId}
          paymentLabel={paymentLabel}
          onDone={() => onOpenChange(false)}
        />
      </DialogContent>
    </Dialog>
  )
}

function ManualMatchForm({
  paymentId,
  paymentLabel,
  onDone,
}: {
  paymentId?: string
  paymentLabel?: string
  onDone: () => void
}) {
  const tenantId = useTenantKey()
  const queryClient = useQueryClient()
  const [selectedPaymentId, setSelectedPaymentId] = useState(paymentId ?? '')
  const [chosenOrderId, setChosenOrderId] = useState('')
  const [paymentSearch, setPaymentSearch] = useState('')
  const [orderSearch, setOrderSearch] = useState('')
  const [note, setNote] = useState('')

  // Query on the settled term, not on every keystroke: each keystroke used to
  // change the query key and drop the list back to a skeleton, which resized
  // the dialog and made it jump while typing.
  const debouncedPaymentSearch = useDebouncedValue(paymentSearch, 250)

  const reconciliationQuery = useQuery({
    queryKey: qk.reconciliationForPayment(tenantId, paymentId),
    queryFn: () => getReconciliationForPayment(paymentId!),
    enabled: Boolean(paymentId),
  })

  const suggestion = reconciliationQuery.data?.find((record) => record.orderId)
  const suggestionIsCurrencyMismatch = suggestion?.reason?.startsWith('Currency mismatch') ?? false
  const suggestionOrderId =
    !suggestionIsCurrencyMismatch && suggestion?.orderId ? suggestion.orderId : ''

  // Derived, not copied into state: the suggestion pre-selects until the user
  // picks something else. No effect, and no stale local copy to keep in sync.
  const effectiveOrderId = chosenOrderId || suggestionOrderId
  const effectiveOrderSearch = orderSearch || (suggestionOrderId ? (suggestion?.orderNumber ?? '') : '')
  const debouncedEffectiveOrderSearch = useDebouncedValue(effectiveOrderSearch, 250)

  const paymentsQuery = useQuery({
    queryKey: qk.payments(tenantId, { picker: 'unmatched', search: debouncedPaymentSearch }),
    queryFn: () =>
      getPayments({ page: 1, pageSize: 8, status: 'Unmatched', search: debouncedPaymentSearch || undefined }),
    enabled: !paymentId,
    // Hold the previous rows while the next search resolves, so the list never
    // collapses mid-typing.
    placeholderData: (previous) => previous,
  })

  const ordersQuery = useQuery({
    queryKey: qk.orders(tenantId, { picker: true, search: debouncedEffectiveOrderSearch }),
    queryFn: () => getOrders({ page: 1, pageSize: 10, search: debouncedEffectiveOrderSearch || undefined }),
    placeholderData: (previous) => previous,
  })

  const mutation = useMutation({
    mutationFn: () =>
      manualMatch({
        paymentId: selectedPaymentId,
        orderId: effectiveOrderId,
        note: note || undefined,
      }),
    onSuccess: (record) => {
      toast.success(`Payment matched to ${record.orderNumber ?? 'order'}`)
      invalidateRelated(queryClient, 'payment')
      onDone()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const canSubmit = selectedPaymentId.length > 0 && effectiveOrderId.length > 0 && !mutation.isPending

  return (
    <>
      <div className="space-y-5">
        <div className="space-y-2">
          <Label>Payment</Label>
          {paymentId ? (
            <div className="flex items-center justify-between rounded-lg border bg-muted/30 px-3 py-2.5">
              <div className="flex items-center gap-2">
                <Link2 className="size-4 text-primary" aria-hidden />
                <span className="text-sm font-medium">{paymentLabel ?? paymentId}</span>
              </div>
              <Badge variant="outline" className="font-mono text-[10px]">
                selected
              </Badge>
            </div>
          ) : (
            <div className="space-y-2">
              <div className="relative">
                <Search
                  className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
                  aria-hidden
                />
                <Input
                  value={paymentSearch}
                  onChange={(event) => setPaymentSearch(event.target.value)}
                  placeholder="Search unmatched payments by reference or customer"
                  className="pl-8"
                />
              </div>
              <div className="h-40 overflow-y-auto rounded-lg border">
                {paymentsQuery.isLoading ? (
                  <div className="space-y-2 p-3" aria-hidden>
                    {Array.from({ length: 4 }).map((_, index) => (
                      <Skeleton key={index} className="h-10 w-full" />
                    ))}
                  </div>
                ) : paymentsQuery.data?.items.length ? (
                  paymentsQuery.data.items.map((payment) => (
                    <button
                      key={payment.id}
                      type="button"
                      onClick={() => setSelectedPaymentId(payment.id)}
                      className={cn(
                        'flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-muted/50',
                        selectedPaymentId === payment.id && 'bg-primary/5',
                      )}
                    >
                      <div className="min-w-0">
                        <p className="truncate font-medium">{payment.providerPaymentId}</p>
                        <p className="truncate text-xs text-muted-foreground">
                          {payment.customerName ?? 'Unknown customer'}
                        </p>
                      </div>
                      <span className="shrink-0 tabular-nums">{formatMoney(payment.amount, payment.currency)}</span>
                      {selectedPaymentId === payment.id ? (
                        <Check className="size-4 shrink-0 text-primary" aria-hidden />
                      ) : null}
                    </button>
                  ))
                ) : (
                  <p className="p-3 text-xs text-muted-foreground">No unmatched payments found.</p>
                )}
              </div>
            </div>
          )}
        </div>

        <div className="space-y-2">
          <Label>Order</Label>
          <div className="relative">
            <Search
              className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden
            />
            <Input
              value={effectiveOrderSearch}
              onChange={(event) => setOrderSearch(event.target.value)}
              placeholder="Search orders by number, customer or reference"
              className="pl-8"
            />
          </div>
          <p className="text-[11px] text-muted-foreground">
            The 10 newest orders are listed until you search. Type an order number or customer to find older orders.
          </p>
          {suggestion?.reason ? (
            <p className="rounded-md border border-dashed bg-muted/30 px-2.5 py-1.5 text-[11px] text-muted-foreground">
              Last match attempt: {suggestion.reason}
            </p>
          ) : null}
          <div className="h-52 overflow-y-auto rounded-lg border">
            {ordersQuery.isLoading ? (
              <div className="space-y-2 p-3" aria-hidden>
                {Array.from({ length: 5 }).map((_, index) => (
                  <Skeleton key={index} className="h-10 w-full" />
                ))}
              </div>
            ) : ordersQuery.data?.items.length ? (
              ordersQuery.data.items.map((order) => (
                <button
                  key={order.id}
                  type="button"
                  onClick={() => setChosenOrderId(order.id)}
                  className={cn(
                    'flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-muted/50',
                    effectiveOrderId === order.id && 'bg-primary/5',
                  )}
                >
                  <div className="min-w-0">
                    <p className="truncate font-medium">{order.orderNumber}</p>
                    <p className="truncate text-xs text-muted-foreground">{order.customerName}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    <span className="tabular-nums">{formatMoney(order.expectedAmount, order.currency)}</span>
                    <StatusBadge tone={orderStatusMeta(order.status).tone}>
                      {orderStatusMeta(order.status).label}
                    </StatusBadge>
                  </div>
                  {effectiveOrderId === order.id ? (
                    <Check className="size-4 shrink-0 text-primary" aria-hidden />
                  ) : null}
                </button>
              ))
            ) : (
              <p className="p-3 text-xs text-muted-foreground">No orders found.</p>
            )}
          </div>
        </div>

        <div className="space-y-2">
          <Label htmlFor="match-note">Note (optional)</Label>
          <Textarea
            id="match-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder="Why is this the right order? e.g. confirmed against remittance advice."
            maxLength={400}
          />
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={onDone} disabled={mutation.isPending}>
          Cancel
        </Button>
        <Button onClick={() => mutation.mutate()} disabled={!canSubmit} loading={mutation.isPending}>
          <Link2 className="size-4" aria-hidden />
          Match payment
        </Button>
      </DialogFooter>
    </>
  )
}
