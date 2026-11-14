import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { LifeBuoy } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { FilterReset } from '@/components/data/filter-reset'
import { ListPagination } from '@/components/data/list-pagination'
import { StatusBadge } from '@/components/data/status-badge'
import { CopyableId } from '@/components/data/copy-button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { CreateTicketDialog } from '@/features/support/components/create-ticket-dialog'
import { getTickets } from '@/features/support/api'
import { errorMessage } from '@/lib/api-client'
import { formatDate } from '@/lib/format'
import {
  supportTicketCategoryMeta,
  supportTicketPriorityMeta,
  supportTicketStatusMeta,
} from '@/lib/status'
import { useTableParams } from '@/lib/use-table-params'
import type { SupportTicketDto } from '@/types/api'

const columns: DataTableColumn<SupportTicketDto>[] = [
  {
    id: 'id',
    header: 'Ticket',
    cell: (ticket) => <CopyableId value={ticket.id.slice(0, 8)} label="ticket id" />,
  },
  {
    id: 'subject',
    header: 'Subject',
    cell: (ticket) => (
      <div className="min-w-0">
        <p className="truncate text-sm font-medium text-foreground">{ticket.subject}</p>
        <p className="mt-0.5 truncate text-xs text-muted-foreground md:hidden">
          {supportTicketCategoryMeta(ticket.category).label}
        </p>
      </div>
    ),
  },
  {
    id: 'category',
    header: 'Category',
    className: 'hidden md:table-cell',
    cell: (ticket) => {
      const meta = supportTicketCategoryMeta(ticket.category)
      return <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
    },
  },
  {
    id: 'priority',
    header: 'Priority',
    cell: (ticket) => {
      const meta = supportTicketPriorityMeta(ticket.priority)
      return <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
    },
  },
  {
    id: 'status',
    header: 'Status',
    cell: (ticket) => {
      const meta = supportTicketStatusMeta(ticket.status)
      return <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
    },
  },
  {
    id: 'created',
    header: 'Created',
    className: 'hidden text-right lg:table-cell',
    cell: (ticket) => <span className="text-xs text-muted-foreground">{formatDate(ticket.createdAt)}</span>,
  },
  {
    id: 'updated',
    header: 'Updated',
    className: 'hidden text-right xl:table-cell',
    cell: (ticket) => <span className="text-xs text-muted-foreground">{formatDate(ticket.updatedAt)}</span>,
  },
]

export function SupportTicketsPage() {
  const navigate = useNavigate()
  const { page, pageSize, get, update, setPage } = useTableParams()
  const status = get('status')
  const category = get('category')
  const priority = get('priority')

  const tickets = useQuery({
    queryKey: ['support', 'tickets', { page, pageSize, status, category, priority }],
    queryFn: () =>
      getTickets({
        page,
        pageSize,
        status: status || undefined,
        category: category || undefined,
        priority: priority || undefined,
      }),
  })

  return (
    <>
      <PageHeader
        title="Support tickets"
        description="Workspace support conversations, newest first."
        actions={<CreateTicketDialog />}
      />

      <FilterBar>
        <Select value={status || 'all'} onValueChange={(value) => update({ status: value === 'all' ? undefined : value })}>
          <SelectTrigger className="h-9 w-[180px]" aria-label="Filter by status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="Open">Open</SelectItem>
            <SelectItem value="InProgress">In progress</SelectItem>
            <SelectItem value="WaitingForCustomer">Waiting for customer</SelectItem>
            <SelectItem value="Resolved">Resolved</SelectItem>
            <SelectItem value="Closed">Closed</SelectItem>
          </SelectContent>
        </Select>

        <Select value={category || 'all'} onValueChange={(value) => update({ category: value === 'all' ? undefined : value })}>
          <SelectTrigger className="h-9 w-[190px]" aria-label="Filter by category">
            <SelectValue placeholder="All categories" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All categories</SelectItem>
            <SelectItem value="TechnicalIssue">Technical issue</SelectItem>
            <SelectItem value="PaymentIssue">Payment issue</SelectItem>
            <SelectItem value="ReconciliationIssue">Reconciliation issue</SelectItem>
            <SelectItem value="AccountIssue">Account issue</SelectItem>
            <SelectItem value="BillingIssue">Billing issue</SelectItem>
            <SelectItem value="Other">Other</SelectItem>
          </SelectContent>
        </Select>

        <Select value={priority || 'all'} onValueChange={(value) => update({ priority: value === 'all' ? undefined : value })}>
          <SelectTrigger className="h-9 w-[160px]" aria-label="Filter by priority">
            <SelectValue placeholder="All priorities" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All priorities</SelectItem>
            <SelectItem value="Low">Low</SelectItem>
            <SelectItem value="Normal">Normal</SelectItem>
            <SelectItem value="High">High</SelectItem>
          </SelectContent>
        </Select>

        <FilterReset
          visible={Boolean(status || category || priority)}
          onClick={() => update({ status: undefined, category: undefined, priority: undefined })}
        />
      </FilterBar>

      {tickets.isError ? (
        <ErrorState message={errorMessage(tickets.error)} onRetry={() => void tickets.refetch()} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={tickets.data?.items ?? []}
            getRowKey={(ticket) => ticket.id}
            caption="Workspace support tickets"
            isLoading={tickets.isLoading}
            onRowClick={(ticket) => navigate(`/support/tickets/${ticket.id}`)}
            empty={
              <EmptyState
                icon={<LifeBuoy className="size-4" aria-hidden />}
                title="No support tickets"
                description="Create a ticket if you need help with payments, reconciliation or the workspace."
                action={<CreateTicketDialog />}
              />
            }
          />
          <ListPagination
            page={page}
            pageSize={pageSize}
            totalCount={tickets.data?.totalCount ?? 0}
            totalPages={tickets.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => update({ pageSize: size }, false)}
          />
        </>
      )}
    </>
  )
}
