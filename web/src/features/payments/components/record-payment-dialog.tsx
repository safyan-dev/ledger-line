import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import { Loader2, Plus } from 'lucide-react'
import { toast } from 'sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { recordPayment } from '@/features/payments/api'
import { errorMessage } from '@/lib/api-client'

const schema = z.object({
  provider: z.string(),
  providerPaymentId: z.string().trim().max(64, 'Provider payment id must be 64 characters or fewer.').optional(),
  amount: z.number().positive('Amount must be greater than zero.'),
  currency: z.string().length(3),
  customerName: z.string().trim().max(160, 'Customer name must be 160 characters or fewer.').optional(),
  customerReference: z.string().trim().max(64, 'Customer reference must be 64 characters or fewer.').optional(),
  reference: z.string().trim().max(64, 'Order reference must be 64 characters or fewer.').optional(),
})

type FormValues = z.infer<typeof schema>

const defaultValues: FormValues = {
  provider: 'manual',
  providerPaymentId: '',
  amount: 0,
  currency: 'USD',
  customerName: '',
  customerReference: '',
  reference: '',
}

export function RecordPaymentDialog() {
  const [open, setOpen] = useState(false)
  const queryClient = useQueryClient()

  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues })

  const mutation = useMutation({
    mutationFn: recordPayment,
    onSuccess: (payment) => {
      toast.success(`Payment ${payment.providerPaymentId} recorded`)
      void queryClient.invalidateQueries({ queryKey: ['payments'] })
      void queryClient.invalidateQueries({ queryKey: ['reconciliation'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
      form.reset(defaultValues)
      setOpen(false)
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const submit = form.handleSubmit((values) => {
    mutation.mutate({
      provider: values.provider,
      providerPaymentId: values.providerPaymentId || undefined,
      amount: values.amount,
      currency: values.currency,
      customerName: values.customerName || undefined,
      customerReference: values.customerReference || undefined,
      reference: values.reference || undefined,
    })
  })

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus className="size-4" aria-hidden />
          Record payment
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Record a payment</DialogTitle>
          <DialogDescription>
            Manual entry runs the same reconciliation pipeline as a provider webhook.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={submit} noValidate>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="provider">Provider</Label>
              <Controller
                control={form.control}
                name="provider"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="provider">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="manual">Manual</SelectItem>
                      <SelectItem value="mock">Mock</SelectItem>
                      <SelectItem value="stripe">Stripe</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="providerPaymentId">Provider payment id</Label>
              <Input
                id="providerPaymentId"
                placeholder="Generated when left blank"
                {...form.register('providerPaymentId')}
              />
              {form.formState.errors.providerPaymentId ? (
                <p className="text-xs text-destructive">{form.formState.errors.providerPaymentId.message}</p>
              ) : null}
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="amount">Amount</Label>
              <Input
                id="amount"
                type="number"
                min="0"
                step="0.01"
                placeholder="400.00"
                {...form.register('amount', { valueAsNumber: true })}
              />
              {form.formState.errors.amount ? (
                <p className="text-xs text-destructive">{form.formState.errors.amount.message}</p>
              ) : null}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="payment-currency">Currency</Label>
              <Controller
                control={form.control}
                name="currency"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="payment-currency">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="USD">USD</SelectItem>
                      <SelectItem value="EUR">EUR</SelectItem>
                      <SelectItem value="GBP">GBP</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="payment-customer">Customer</Label>
            <Input id="payment-customer" placeholder="Acme Industries" {...form.register('customerName')} />
            {form.formState.errors.customerName ? (
              <p className="text-xs text-destructive">{form.formState.errors.customerName.message}</p>
            ) : null}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="payment-customer-ref">Customer reference</Label>
              <Input id="payment-customer-ref" placeholder="ACME-77" {...form.register('customerReference')} />
              {form.formState.errors.customerReference ? (
                <p className="text-xs text-destructive">{form.formState.errors.customerReference.message}</p>
              ) : null}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="payment-reference">Order reference</Label>
              <Input id="payment-reference" placeholder="ORD-2026-0001" {...form.register('reference')} />
              {form.formState.errors.reference ? (
                <p className="text-xs text-destructive">{form.formState.errors.reference.message}</p>
              ) : null}
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={mutation.isPending}>
              Cancel
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Plus className="size-4" aria-hidden />}
              Record payment
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
