import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Undo2 } from 'lucide-react'
import { toast } from 'sonner'
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
import { Textarea } from '@/components/ui/textarea'
import { refundPayment } from '@/features/payments/api'
import { errorMessage } from '@/lib/api-client'
import { formatMoney } from '@/lib/format'
import { invalidateRelated } from '@/lib/invalidate'

export function RefundDialog({
  open,
  onOpenChange,
  paymentId,
  maxAmount,
  currency = 'USD',
  paymentLabel,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  paymentId?: string
  maxAmount: number
  currency?: string
  paymentLabel?: string
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Refund payment</DialogTitle>
          <DialogDescription>
            {paymentLabel ? `${paymentLabel}. ` : ''}
            Manual refunds are recorded as succeeded and the order allocation is reversed. Provider refunds are
            handled by your finance team outside Ledgerline.
          </DialogDescription>
        </DialogHeader>

        {/* DialogContent unmounts when closed, so the form initialises fresh on
            every open — no effect needed to reset state. */}
        <RefundForm
          paymentId={paymentId}
          maxAmount={maxAmount}
          currency={currency}
          onDone={() => onOpenChange(false)}
        />
      </DialogContent>
    </Dialog>
  )
}

function RefundForm({
  paymentId,
  maxAmount,
  currency,
  onDone,
}: {
  paymentId?: string
  maxAmount: number
  currency: string
  onDone: () => void
}) {
  const queryClient = useQueryClient()
  const [amount, setAmount] = useState(() => (maxAmount > 0 ? maxAmount.toFixed(2) : ''))
  const [reason, setReason] = useState('')

  const amountValue = Number(amount)

  const mutation = useMutation({
    mutationFn: () =>
      refundPayment(paymentId!, { amount: amountValue, reason: reason.trim() }),
    onSuccess: () => {
      toast.success('Refund recorded')
      invalidateRelated(queryClient, 'payment')
      onDone()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const canSubmit =
    Boolean(paymentId) &&
    Number.isFinite(amountValue) &&
    amountValue > 0 &&
    amountValue <= maxAmount &&
    reason.trim().length > 0 &&
    !mutation.isPending

  return (
    <>
      <div className="space-y-4">
          <div className="rounded-lg border bg-muted/30 px-3 py-2 text-sm">
            <p className="text-xs text-muted-foreground">Refundable amount</p>
            <p className="mt-0.5 text-lg font-semibold tabular-nums">{formatMoney(maxAmount, currency)}</p>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="refund-amount">Refund amount</Label>
            <Input
              id="refund-amount"
              type="number"
              min="0.01"
              max={maxAmount}
              step="0.01"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
            />
            <p className="text-[11px] text-muted-foreground">
              Partial refunds are allowed. The value cannot exceed the refundable amount.
            </p>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="refund-reason">Reason</Label>
            <Textarea
              id="refund-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Duplicate payment / customer overpayment / agreed refund."
              maxLength={400}
            />
          </div>
        </div>

      <DialogFooter>
        <Button variant="outline" onClick={onDone} disabled={mutation.isPending}>
          Cancel
        </Button>
        <Button onClick={() => mutation.mutate()} disabled={!canSubmit} loading={mutation.isPending}>
          <Undo2 className="size-4" aria-hidden />
          Record refund
        </Button>
      </DialogFooter>
    </>
  )
}
