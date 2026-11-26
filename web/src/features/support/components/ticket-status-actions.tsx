import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Workflow } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { changeTicketStatus } from '@/features/support/api'
import { errorMessage } from '@/lib/api-client'
import { supportTicketStatusMeta } from '@/lib/status'
import { invalidateRelated } from '@/lib/invalidate'

const transitions: Record<string, string[]> = {
  Open: ['InProgress', 'WaitingForCustomer', 'Resolved'],
  InProgress: ['WaitingForCustomer', 'Resolved'],
  WaitingForCustomer: ['InProgress', 'Resolved'],
  Resolved: ['Closed', 'Open'],
  Closed: ['Open'],
}

export function TicketStatusActions({
  ticketId,
  status,
}: {
  ticketId: string
  status: string
}) {
  const queryClient = useQueryClient()
  const options = transitions[status] ?? []
  const [nextStatus, setNextStatus] = useState(options[0] ?? '')

  const mutation = useMutation({
    mutationFn: () => changeTicketStatus(ticketId, nextStatus),
    onSuccess: () => {
      toast.success('Ticket status updated.')
      invalidateRelated(queryClient, 'ticket')
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  if (options.length === 0) {
    return <p className="text-xs text-muted-foreground">No further status changes are available.</p>
  }

  return (
    <div className="flex flex-wrap items-end gap-2">
      <Select value={nextStatus} onValueChange={setNextStatus}>
        <SelectTrigger className="h-9 w-[200px]" aria-label="Next ticket status">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {options.map((option) => {
            const meta = supportTicketStatusMeta(option)
            return (
              <SelectItem key={option} value={option}>
                {meta.label}
              </SelectItem>
            )
          })}
        </SelectContent>
      </Select>
      <Button type="button" onClick={() => mutation.mutate()} disabled={!nextStatus} loading={mutation.isPending}>
        <Workflow className="size-4" aria-hidden />
        Update status
      </Button>
    </div>
  )
}
