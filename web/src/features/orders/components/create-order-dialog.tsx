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
import { Textarea } from '@/components/ui/textarea'
import { createOrder } from '@/features/orders/api'
import { errorMessage } from '@/lib/api-client'

const schema = z.object({
  customerName: z.string().trim().min(1, 'Customer name is required.').max(160, 'Customer name must be 160 characters or fewer.'),
  customerReference: z.string().trim().max(64, 'Customer reference must be 64 characters or fewer.').optional(),
  description: z.string().trim().max(400, 'Description must be 400 characters or fewer.').optional(),
  expectedAmount: z.number().positive('Amount must be greater than zero.'),
  currency: z.string().length(3),
})

type FormValues = z.infer<typeof schema>

const defaultValues: FormValues = {
  customerName: '',
  customerReference: '',
  description: '',
  expectedAmount: 0,
  currency: 'USD',
}

export function CreateOrderDialog({
  open: controlledOpen,
  onOpenChange,
  showTrigger = true,
}: {
  open?: boolean
  onOpenChange?: (open: boolean) => void
  showTrigger?: boolean
}) {
  const [internalOpen, setInternalOpen] = useState(false)
  const open = controlledOpen ?? internalOpen
  const setOpen = onOpenChange ?? setInternalOpen
  const queryClient = useQueryClient()

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues,
    mode: 'onSubmit',
  })

  const mutation = useMutation({
    mutationFn: createOrder,
    onSuccess: (order) => {
      toast.success(`Order ${order.orderNumber} created`)
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      form.reset(defaultValues)
      setOpen(false)
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      customerName: values.customerName,
      customerReference: values.customerReference || undefined,
      description: values.description || undefined,
      expectedAmount: values.expectedAmount,
      currency: values.currency,
    })
  })

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      {showTrigger ? (
        <DialogTrigger asChild>
          <Button>
            <Plus className="size-4" aria-hidden />
            New order
          </Button>
        </DialogTrigger>
      ) : null}
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Create an order</DialogTitle>
          <DialogDescription>
            The expected amount is what reconciliation compares incoming payments against.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={onSubmit} noValidate>
          <div className="space-y-1.5">
            <Label htmlFor="customerName">Customer</Label>
            <Input id="customerName" placeholder="Northwind Traders" {...form.register('customerName')} />
            {form.formState.errors.customerName ? (
              <p className="text-xs text-destructive">{form.formState.errors.customerName.message}</p>
            ) : null}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="customerReference">Customer reference</Label>
              <Input id="customerReference" placeholder="NW-5512" {...form.register('customerReference')} />
              {form.formState.errors.customerReference ? (
                <p className="text-xs text-destructive">{form.formState.errors.customerReference.message}</p>
              ) : null}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="expectedAmount">Expected amount</Label>
              <Input
                id="expectedAmount"
                type="number"
                min="0"
                step="0.01"
                placeholder="1250.00"
                {...form.register('expectedAmount', { valueAsNumber: true })}
              />
              {form.formState.errors.expectedAmount ? (
                <p className="text-xs text-destructive">{form.formState.errors.expectedAmount.message}</p>
              ) : null}
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="currency">Currency</Label>
            <Controller
              control={form.control}
              name="currency"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="currency">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="USD">USD — US dollar</SelectItem>
                    <SelectItem value="EUR">EUR — Euro</SelectItem>
                    <SelectItem value="GBP">GBP — Pound sterling</SelectItem>
                  </SelectContent>
                </Select>
              )}
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="description">Description</Label>
            <Textarea id="description" placeholder="Q4 platform licence" maxLength={400} {...form.register('description')} />
            {form.formState.errors.description ? (
              <p className="text-xs text-destructive">{form.formState.errors.description.message}</p>
            ) : null}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={mutation.isPending}>
              Cancel
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Plus className="size-4" aria-hidden />}
              Create order
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
