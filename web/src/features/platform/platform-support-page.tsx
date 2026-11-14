import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LifeBuoy, Send } from 'lucide-react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { ConfirmDialog } from '@/components/data/confirm-dialog'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { ListPagination } from '@/components/data/list-pagination'
import { SearchInput } from '@/components/data/search-input'
import { StatusBadge } from '@/components/data/status-badge'
import { PageHeader } from '@/components/layout/page-header'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { Skeleton } from '@/components/ui/skeleton'
import {
  getPlatformSupportTicket,
  getPlatformSupportTickets,
  replyToPlatformSupportTicket,
  updatePlatformSupportTicketStatus,
} from '@/features/platform/api'
import { errorMessage } from '@/lib/api-client'
import { formatDateTime } from '@/lib/format'
import { useTableParams } from '@/lib/use-table-params'
import type { PlatformSupportTicketDto } from '@/types/api'

function allowedTicketTransitions(status: string): string[] {
  switch (status) {
    case 'Open':
      return ['InProgress', 'WaitingForCustomer', 'Resolved']
    case 'InProgress':
      return ['WaitingForCustomer', 'Resolved']
    case 'WaitingForCustomer':
      return ['InProgress', 'Resolved']
    case 'Resolved':
      return ['Closed', 'Open']
    case 'Closed':
      return ['Open']
    default:
      return []
  }
}

function ticketStatusLabel(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function PlatformSupportTicketDialog({
  ticket,
  onClose,
}: {
  ticket: PlatformSupportTicketDto | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [reply, setReply] = useState('')
  const [nextStatus, setNextStatus] = useState('')
  const [confirmStatus, setConfirmStatus] = useState('')
  const detail = useQuery({
    queryKey: ['platform', 'support-ticket', ticket?.id],
    queryFn: () => getPlatformSupportTicket(ticket!.id),
    enabled: Boolean(ticket),
  })

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['platform', 'support-tickets'] }),
      queryClient.invalidateQueries({ queryKey: ['platform', 'support-ticket', ticket?.id] }),
    ])
  }

  const replyMutation = useMutation({
    mutationFn: () => {
      if (!ticket) throw new Error('Select a support ticket first.')
      return replyToPlatformSupportTicket(ticket.id, reply.trim())
    },
    onSuccess: async () => {
      toast.success('Reply sent to the customer.')
      setReply('')
      await invalidate()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })
  const statusMutation = useMutation({
    mutationFn: () => {
      if (!ticket || !confirmStatus) throw new Error('Choose a valid ticket status.')
      return updatePlatformSupportTicketStatus(ticket.id, confirmStatus)
    },
    onSuccess: async () => {
      toast.success('Support ticket status updated.')
      setConfirmStatus('')
      setNextStatus('')
      await invalidate()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const ticketData = detail.data?.ticket
  const transitions = ticketData ? allowedTicketTransitions(ticketData.status) : []

  return (
    <>
      <Dialog
        open={ticket !== null}
        onOpenChange={(open) => {
          if (!open) onClose()
        }}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{ticket?.subject ?? 'Support ticket'}</DialogTitle>
            <DialogDescription>
              {detail.data
                ? `${detail.data.tenantName} · ${ticket?.id}`
                : 'Ticket details across customer workspaces'}
            </DialogDescription>
          </DialogHeader>

          {detail.isError ? (
            <ErrorState message={errorMessage(detail.error)} onRetry={() => void detail.refetch()} />
          ) : detail.isLoading || !detail.data || !ticketData ? (
            <div className="space-y-3" aria-busy="true" aria-label="Loading support ticket">
              <Skeleton className="h-28 rounded-lg" />
              <Skeleton className="h-48 rounded-lg" />
            </div>
          ) : (
            <div className="space-y-5">
              <div className="grid gap-3 rounded-lg border border-border/80 bg-muted/20 p-4 sm:grid-cols-2">
                <p className="text-sm">
                  <span className="text-xs text-muted-foreground">Tenant</span><br />
                  <Link className="font-medium text-primary hover:underline" to={`/platform/tenants/${detail.data.tenantId}`} onClick={onClose}>
                    {detail.data.tenantName}
                  </Link>
                </p>
                <p className="text-sm">
                  <span className="text-xs text-muted-foreground">Created by</span><br />
                  <span className="font-medium text-foreground">{ticketData.createdBy}</span>
                </p>
                <p className="text-sm">
                  <span className="text-xs text-muted-foreground">Category / priority</span><br />
                  <span className="font-medium text-foreground">
                    {ticketData.category} · {ticketData.priority}
                  </span>
                </p>
                <p className="text-sm">
                  <span className="text-xs text-muted-foreground">Status</span><br />
                  <StatusBadge tone={ticketData.status === 'Resolved' || ticketData.status === 'Closed' ? 'success' : 'warning'}>
                    {ticketStatusLabel(ticketData.status)}
                  </StatusBadge>
                </p>
              </div>

              <section>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Customer request</h3>
                <p className="mt-2 whitespace-pre-wrap rounded-lg border border-border/80 p-3 text-sm text-foreground">
                  {ticketData.description}
                </p>
              </section>

              <section>
                <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Conversation
                </h3>
                {detail.data.messages.length === 0 ? (
                  <p className="rounded-lg border border-dashed p-4 text-center text-sm text-muted-foreground">
                    No replies yet.
                  </p>
                ) : (
                  <ol className="max-h-64 space-y-3 overflow-y-auto">
                    {detail.data.messages.map((message) => (
                      <li key={message.id} className="rounded-lg border border-border/80 p-3">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <p className="text-xs font-semibold text-foreground">
                            {message.authorName}
                            <span className="ml-1 font-normal text-muted-foreground">· {message.authorRole}</span>
                          </p>
                          <time className="text-[11px] text-muted-foreground">{formatDateTime(message.createdAt)}</time>
                        </div>
                        <p className="mt-2 whitespace-pre-wrap text-sm text-foreground">{message.body}</p>
                      </li>
                    ))}
                  </ol>
                )}
              </section>

              <form
                className="space-y-2"
                onSubmit={(event) => {
                  event.preventDefault()
                  replyMutation.mutate()
                }}
              >
                <label htmlFor="platform-ticket-reply" className="text-xs font-medium text-muted-foreground">
                  Reply to customer
                </label>
                <Textarea
                  id="platform-ticket-reply"
                  value={reply}
                  onChange={(event) => setReply(event.target.value)}
                  placeholder="Write a customer-visible reply…"
                  maxLength={4000}
                  rows={4}
                  disabled={ticketData.status === 'Closed' || replyMutation.isPending}
                />
                <div className="flex justify-end">
                  <Button type="submit" disabled={!reply.trim() || ticketData.status === 'Closed' || replyMutation.isPending}>
                    <Send className="size-3.5" aria-hidden />
                    Send reply
                  </Button>
                </div>
              </form>

              <div className="flex flex-wrap items-end gap-2 border-t border-border/70 pt-4">
                <label className="min-w-44 flex-1 text-xs font-medium text-muted-foreground">
                  Update status
                  <Select value={nextStatus} onValueChange={setNextStatus}>
                    <SelectTrigger className="mt-1 w-full" aria-label="New support ticket status">
                      <SelectValue placeholder="Choose a transition" />
                    </SelectTrigger>
                    <SelectContent>
                      {transitions.map((status) => (
                        <SelectItem key={status} value={status}>{ticketStatusLabel(status)}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </label>
                <Button
                  variant="outline"
                  disabled={!nextStatus || statusMutation.isPending}
                  onClick={() => setConfirmStatus(nextStatus)}
                >
                  Update status
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={Boolean(confirmStatus)}
        onOpenChange={(open) => {
          if (!open) setConfirmStatus('')
        }}
        title={`Change ticket status to ${ticketStatusLabel(confirmStatus)}?`}
        description="The status change will be visible in the tenant support conversation."
        confirmLabel="Update status"
        pending={statusMutation.isPending}
        onConfirm={() => statusMutation.mutate()}
      />
    </>
  )
}

export function PlatformSupportPage() {
  const [selectedTicket, setSelectedTicket] = useState<PlatformSupportTicketDto | null>(null)
  const table = useTableParams(25)
  const status = table.get('status')
  const category = table.get('category')
  const priority = table.get('priority')
  const search = table.get('search')
  const tickets = useQuery({
    queryKey: ['platform', 'support-tickets', table.page, table.pageSize, status, category, priority, search],
    queryFn: () =>
      getPlatformSupportTickets({
        page: table.page,
        pageSize: table.pageSize,
        status,
        category,
        priority,
        search,
      }),
  })

  const columns: DataTableColumn<PlatformSupportTicketDto>[] = [
    {
      id: 'tenant',
      header: 'Tenant',
      cell: (item) => (
        <Link
          to={`/platform/tenants/${item.tenantId}`}
          className="text-sm font-medium text-foreground hover:text-primary"
        >
          {item.tenantName}
        </Link>
      ),
    },
    {
      id: 'ticket',
      header: 'Ticket',
      cell: (item) => (
        <div className="max-w-64">
          <p className="truncate text-sm font-medium text-foreground">{item.subject}</p>
          <p className="font-mono text-[11px] text-muted-foreground">{item.id}</p>
        </div>
      ),
    },
    { id: 'category', header: 'Category', cell: (item) => <span className="text-xs">{item.category}</span> },
    {
      id: 'priority',
      header: 'Priority',
      cell: (item) => (
        <StatusBadge tone={item.priority === 'High' ? 'danger' : item.priority === 'Normal' ? 'warning' : 'neutral'}>
          {item.priority}
        </StatusBadge>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      cell: (item) => (
        <StatusBadge tone={item.status === 'Resolved' || item.status === 'Closed' ? 'success' : 'info'}>
          {ticketStatusLabel(item.status)}
        </StatusBadge>
      ),
    },
    { id: 'created', header: 'Created', cell: (item) => <span className="text-xs">{formatDateTime(item.createdAt)}</span> },
    {
      id: 'actions',
      header: '',
      className: 'text-right',
      cell: (item) => (
        <Button size="sm" variant="outline" onClick={() => setSelectedTicket(item)}>
          Open ticket
        </Button>
      ),
    },
  ]

  return (
    <div>
      <PageHeader title="Support" description="Customer support requests from across all tenants." />

      <FilterBar>
        <SearchInput
          defaultValue={search}
          onCommit={(value) => table.update({ search: value })}
          placeholder="Search subject or tenant…"
        />
        <Select value={status || 'All'} onValueChange={(value) => table.update({ status: value === 'All' ? '' : value })}>
          <SelectTrigger className="w-full sm:w-48" aria-label="Filter support ticket status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All statuses</SelectItem>
            {['Open', 'InProgress', 'WaitingForCustomer', 'Resolved', 'Closed'].map((value) => (
              <SelectItem key={value} value={value}>{ticketStatusLabel(value)}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={category || 'All'} onValueChange={(value) => table.update({ category: value === 'All' ? '' : value })}>
          <SelectTrigger className="w-full sm:w-52" aria-label="Filter support ticket category">
            <SelectValue placeholder="All categories" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All categories</SelectItem>
            {['TechnicalIssue', 'PaymentIssue', 'ReconciliationIssue', 'AccountIssue', 'BillingIssue', 'Other'].map((value) => (
              <SelectItem key={value} value={value}>{ticketStatusLabel(value)}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={priority || 'All'} onValueChange={(value) => table.update({ priority: value === 'All' ? '' : value })}>
          <SelectTrigger className="w-full sm:w-40" aria-label="Filter support ticket priority">
            <SelectValue placeholder="All priorities" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All priorities</SelectItem>
            {['Low', 'Normal', 'High'].map((value) => (
              <SelectItem key={value} value={value}>{value}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </FilterBar>

      {tickets.isError ? (
        <ErrorState message={errorMessage(tickets.error)} onRetry={() => void tickets.refetch()} />
      ) : tickets.isLoading || !tickets.data ? (
        <div className="space-y-3" aria-busy="true" aria-label="Loading platform support tickets">
          <Skeleton className="h-12 rounded-lg" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={tickets.data.items}
            getRowKey={(item) => item.id}
            caption="Support tickets across tenants"
            empty={
              <EmptyState
                icon={<LifeBuoy className="size-4" aria-hidden />}
                title="No support tickets found"
                description="Tickets matching these filters will appear here."
              />
            }
          />
          {tickets.data.totalCount > 0 ? (
            <ListPagination
              page={tickets.data.page}
              pageSize={tickets.data.pageSize}
              totalCount={tickets.data.totalCount}
              totalPages={tickets.data.totalPages}
              onPageChange={table.setPage}
              onPageSizeChange={table.setPageSize}
              className="px-1"
            />
          ) : null}
        </>
      )}

      <PlatformSupportTicketDialog ticket={selectedTicket} onClose={() => setSelectedTicket(null)} />
    </div>
  )
}
