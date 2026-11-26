import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Webhook } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { ListPagination } from '@/components/data/list-pagination'
import { StatusBadge } from '@/components/data/status-badge'
import { SearchInput } from '@/components/data/search-input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { WebhookDetailSheet } from '@/features/webhooks/components/webhook-detail-sheet'
import { getWebhookEvents } from '@/features/webhooks/api'
import { formatDateTime } from '@/lib/format'
import { webhookStatusMeta } from '@/lib/status'
import { useTableParams } from '@/lib/use-table-params'
import type { WebhookEventListItemDto } from '@/types/api'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'
import { errorStateProps } from '@/lib/api-client'

const eventTypes = ['payment.succeeded', 'customer.created']

const columns: DataTableColumn<WebhookEventListItemDto>[] = [
  {
    id: 'event',
    header: 'Event',
    cell: (event) => (
      <div className="min-w-0">
        <p className="font-mono text-xs font-medium text-foreground">{event.providerEventId}</p>
        <p className="mt-0.5 text-[11px] text-muted-foreground">{event.eventType}</p>
        {event.duplicateCount > 0 || event.conflictCount > 0 ? (
          <p className="mt-0.5 text-[11px] text-muted-foreground">
            {event.duplicateCount > 0 ? `dup ${event.duplicateCount}` : null}
            {event.duplicateCount > 0 && event.conflictCount > 0 ? ' · ' : null}
            {event.conflictCount > 0 ? (
              <span className="text-destructive">conflict {event.conflictCount}</span>
            ) : null}
          </p>
        ) : null}
      </div>
    ),
  },
  {
    id: 'provider',
    header: 'Provider',
    cell: (event) => <span className="text-sm capitalize text-foreground">{event.provider}</span>,
  },
  {
    id: 'status',
    header: 'Status',
    cell: (event) => (
      <StatusBadge tone={webhookStatusMeta(event.status).tone}>{webhookStatusMeta(event.status).label}</StatusBadge>
    ),
  },
  {
    id: 'attempts',
    header: 'Attempts',
    className: 'text-right',
    cell: (event) => <span className="tabular-nums text-sm">{event.attemptCount}</span>,
  },
  {
    id: 'received',
    header: 'Received',
    className: 'text-right whitespace-nowrap',
    cell: (event) => <span className="text-xs text-muted-foreground">{formatDateTime(event.receivedAt)}</span>,
  },
  {
    id: 'duration',
    header: 'Processing',
    className: 'hidden text-right whitespace-nowrap lg:table-cell',
    cell: (event) =>
      event.processedAt ? (
        <span className="text-xs text-muted-foreground">
          {Math.max(0, new Date(event.processedAt).getTime() - new Date(event.receivedAt).getTime())} ms
        </span>
      ) : (
        <span className="text-xs text-muted-foreground">—</span>
      ),
  },
]

export function WebhooksPage() {
  const tenantId = useTenantKey()
  const { page, pageSize, get, update, setPage } = useTableParams()
  const status = get('status')
  const eventType = get('eventType')
  const search = get('search')
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)

  const events = useQuery({
    queryKey: qk.webhooks(tenantId, { page, pageSize, status, eventType, search }),
    queryFn: () =>
      getWebhookEvents({
        page,
        pageSize,
        status: status || undefined,
        eventType: eventType || undefined,
        search: search || undefined,
      }),
  })

  return (
    <>
      <PageHeader
        title="Webhook events"
        description="Provider notifications as they arrived, their signature state and processing outcome."
      />

      <FilterBar>
        <SearchInput
          key={search}
          defaultValue={search}
          onCommit={(value) => update({ search: value || undefined })}
          placeholder="Search event id"
        />
        <Select value={status || 'all'} onValueChange={(value) => update({ status: value === 'all' ? undefined : value })}>
          <SelectTrigger className="h-9 w-[170px]" aria-label="Filter by webhook status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="Received">Received</SelectItem>
            <SelectItem value="Processing">Processing</SelectItem>
            <SelectItem value="Processed">Processed</SelectItem>
            <SelectItem value="Failed">Failed</SelectItem>
            <SelectItem value="Ignored">Ignored</SelectItem>
            <SelectItem value="Conflict">Conflict</SelectItem>
          </SelectContent>
        </Select>
        <Select
          value={eventType || 'all'}
          onValueChange={(value) => update({ eventType: value === 'all' ? undefined : value })}
        >
          <SelectTrigger className="h-9 w-[190px]" aria-label="Filter by event type">
            <SelectValue placeholder="All event types" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All event types</SelectItem>
            {eventTypes.map((type) => (
              <SelectItem key={type} value={type}>
                {type}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </FilterBar>

      {events.isError ? (
        <ErrorState {...errorStateProps(events.error)} onRetry={() => void events.refetch()} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={events.data?.items ?? []}
            getRowKey={(event) => event.id}
            caption="Incoming provider webhook events"
            isLoading={events.isLoading}
            onRowClick={(event) => setSelectedEventId(event.id)}
            empty={
              <EmptyState
                icon={<Webhook className="size-4" aria-hidden />}
                title="No webhook events"
                description="Send a signed payload with scripts/send-webhook.sh to exercise ingestion."
              />
            }
          />
          <ListPagination
            page={page}
            pageSize={pageSize}
            totalCount={events.data?.totalCount ?? 0}
            totalPages={events.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => update({ pageSize: size }, false)}
          />
        </>
      )}

      <WebhookDetailSheet
        eventId={selectedEventId}
        onOpenChange={(open) => {
          if (!open) setSelectedEventId(null)
        }}
      />
    </>
  )
}
