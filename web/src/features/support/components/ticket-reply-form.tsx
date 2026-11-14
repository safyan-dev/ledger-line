import { useMutation, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, Send } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { replyToTicket } from '@/features/support/api'
import { errorMessage } from '@/lib/api-client'

const schema = z.object({
  body: z.string().trim().min(1, 'Enter a reply').max(4000, 'Use 4000 characters or fewer'),
})

type FormValues = z.infer<typeof schema>

export function TicketReplyForm({ ticketId }: { ticketId: string }) {
  const queryClient = useQueryClient()
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { body: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: FormValues) => replyToTicket(ticketId, values.body),
    onSuccess: () => {
      toast.success('Reply added.')
      form.reset({ body: '' })
      void queryClient.invalidateQueries({ queryKey: ['support'] })
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const onSubmit = form.handleSubmit((values) => mutation.mutate(values))

  return (
    <form className="space-y-3" onSubmit={onSubmit} noValidate>
      <div className="space-y-1.5">
        <Label htmlFor="ticketReply">Reply</Label>
        <Textarea id="ticketReply" rows={4} maxLength={4000} placeholder="Add an update or answer…" {...form.register('body')} />
        {form.formState.errors.body ? (
          <p className="text-xs text-destructive">{form.formState.errors.body.message}</p>
        ) : null}
      </div>
      <Button type="submit" disabled={mutation.isPending}>
        {mutation.isPending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Send className="size-4" aria-hidden />}
        Send reply
      </Button>
    </form>
  )
}
