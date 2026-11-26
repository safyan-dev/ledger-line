import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { Plus } from 'lucide-react'
import { Controller, useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
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
import { createTicket } from '@/features/support/api'
import { errorMessage } from '@/lib/api-client'
import { invalidateRelated } from '@/lib/invalidate'

const schema = z.object({
  subject: z.string().trim().min(1, 'Enter a subject').max(200, 'Use 200 characters or fewer'),
  category: z.enum([
    'TechnicalIssue',
    'PaymentIssue',
    'ReconciliationIssue',
    'AccountIssue',
    'BillingIssue',
    'Other',
  ]),
  priority: z.enum(['Low', 'Normal', 'High']),
  description: z.string().trim().min(1, 'Describe the issue').max(4000, 'Use 4000 characters or fewer'),
})

type FormValues = z.infer<typeof schema>

const defaultValues: FormValues = {
  subject: '',
  category: 'TechnicalIssue',
  priority: 'Normal',
  description: '',
}

export function CreateTicketDialog() {
  const [open, setOpen] = useState(false)
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues,
  })

  const mutation = useMutation({
    mutationFn: createTicket,
    onSuccess: (ticket) => {
      toast.success('Support ticket created.')
      form.reset(defaultValues)
      setOpen(false)
      invalidateRelated(queryClient, 'ticket')
      navigate(`/support/tickets/${ticket.id}`)
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const onSubmit = form.handleSubmit((values) => mutation.mutate(values))

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus className="size-4" aria-hidden />
          New ticket
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Create a support ticket</DialogTitle>
          <DialogDescription>
            Describe the issue so the workspace owner can review and respond.
          </DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={onSubmit} noValidate>
          <div className="space-y-1.5">
            <Label htmlFor="ticketSubject">Subject</Label>
            <Input id="ticketSubject" {...form.register('subject')} />
            {form.formState.errors.subject ? (
              <p className="text-xs text-destructive">{form.formState.errors.subject.message}</p>
            ) : null}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="ticketCategory">Category</Label>
              <Controller
                control={form.control}
                name="category"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="ticketCategory">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="TechnicalIssue">Technical issue</SelectItem>
                      <SelectItem value="PaymentIssue">Payment issue</SelectItem>
                      <SelectItem value="ReconciliationIssue">Reconciliation issue</SelectItem>
                      <SelectItem value="AccountIssue">Account issue</SelectItem>
                      <SelectItem value="BillingIssue">Billing issue</SelectItem>
                      <SelectItem value="Other">Other</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="ticketPriority">Priority</Label>
              <Controller
                control={form.control}
                name="priority"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="ticketPriority">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Low">Low</SelectItem>
                      <SelectItem value="Normal">Normal</SelectItem>
                      <SelectItem value="High">High</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="ticketDescription">Description</Label>
            <Textarea id="ticketDescription" rows={6} maxLength={4000} {...form.register('description')} />
            {form.formState.errors.description ? (
              <p className="text-xs text-destructive">{form.formState.errors.description.message}</p>
            ) : null}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={mutation.isPending}>
              Cancel
            </Button>
            <Button type="submit" loading={mutation.isPending}>
              <Plus className="size-4" aria-hidden />
              Create ticket
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
