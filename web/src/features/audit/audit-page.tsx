import { useQuery } from '@tanstack/react-query'
import { ScrollText, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { PageHeader } from '@/components/layout/page-header'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { ListPagination } from '@/components/data/list-pagination'
import { SearchInput } from '@/components/data/search-input'
import { StatusBadge } from '@/components/data/status-badge'
import { getAuditLogs } from '@/features/audit/api'
import { formatDateTime, initials } from '@/lib/format'
import { actionLabel } from '@/lib/status'
import { useTableParams } from '@/lib/use-table-params'
import type { AuditLogDto } from '@/types/api'

const columns: DataTableColumn<AuditLogDto>[] = [
  {
    id: 'when',
    header: 'When',
    className: 'whitespace-nowrap',
    cell: (log) => <span className="text-xs text-muted-foreground">{formatDateTime(log.occurredAt)}</span>,
  },
  {
    id: 'actor',
    header: 'Actor',
    cell: (log) => (
      <div className="flex items-center gap-2">
        <span className="flex size-6 items-center justify-center rounded-full bg-muted text-[10px] font-semibold text-muted-foreground">
          {initials(log.actorName)}
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm text-foreground">{log.actorName}</p>
          <p className="text-[11px] text-muted-foreground">{log.actorRole}</p>
        </div>
      </div>
    ),
  },
  {
    id: 'action',
    header: 'Action',
    cell: (log) => (
      <StatusBadge
        tone={
          log.action.includes('failed') || log.action.includes('cancelled')
            ? 'danger'
            : log.action.includes('unmatched') || log.action.includes('ambiguous')
              ? 'warning'
              : 'info'
        }
      >
        {actionLabel(log.action)}
      </StatusBadge>
    ),
  },
  {
    id: 'summary',
    header: 'Summary',
    className: 'max-w-[340px] overflow-hidden',
    cell: (log) => <p className="truncate text-sm text-foreground">{log.summary}</p>,
  },
  {
    id: 'entity',
    header: 'Entity',
    className: 'hidden text-right lg:table-cell',
    cell: (log) => (
      <div className="text-right">
        <p className="text-xs font-medium text-foreground">{log.entityType}</p>
        <p className="font-mono text-[10px] text-muted-foreground">{log.entityId?.slice(0, 8) ?? '—'}</p>
      </div>
    ),
  },
]

export function AuditPage() {
  const { page, pageSize, get, update, setPage } = useTableParams()
  const module = get('module')
  const search = get('search')
  const entityType = get('entityType')
  const entityId = get('entityId')

  const logs = useQuery({
    queryKey: ['audit', { page, pageSize, module, search, entityType, entityId }],
    queryFn: () =>
      getAuditLogs({
        page,
        pageSize,
        module: module || undefined,
        search: search || undefined,
        entityType: entityType || undefined,
        entityId: entityId || undefined,
      }),
  })

  return (
    <>
      <PageHeader
        title="Audit history"
        description="Append-only history of the business actions that changed money or state."
      />

      <FilterBar>
        <SearchInput
          key={search}
          defaultValue={search}
          onCommit={(value) => update({ search: value || undefined })}
          placeholder="Search audit log"
        />
        <Select value={module || 'all'} onValueChange={(value) => update({ module: value === 'all' ? undefined : value })}>
          <SelectTrigger className="h-9 w-[170px]" aria-label="Filter by module">
            <SelectValue placeholder="All modules" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All modules</SelectItem>
            <SelectItem value="orders">Orders</SelectItem>
            <SelectItem value="payments">Payments</SelectItem>
            <SelectItem value="webhooks">Webhooks</SelectItem>
            <SelectItem value="reconciliation">Reconciliation</SelectItem>
          </SelectContent>
        </Select>
        {entityId ? (
          <span className="inline-flex h-9 items-center gap-2 rounded-md border bg-muted/40 px-2.5 text-xs text-muted-foreground">
            {entityType || 'Entity'} {entityId.slice(0, 8)}
            <Button
              variant="ghost"
              size="icon"
              className="size-5 text-muted-foreground"
              onClick={() => update({ entityType: undefined, entityId: undefined })}
              aria-label="Clear entity filter"
            >
              <X className="size-3" aria-hidden />
            </Button>
          </span>
        ) : null}
      </FilterBar>

      {logs.isError ? (
        <ErrorState message={logs.error.message} onRetry={() => void logs.refetch()} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={logs.data?.items ?? []}
            getRowKey={(log) => log.id}
            caption="Audit log entries ordered newest first"
            isLoading={logs.isLoading}
            empty={
              <EmptyState
                icon={<ScrollText className="size-4" aria-hidden />}
                title="No audit entries"
                description="Business actions appear here as they are committed."
              />
            }
          />
          <ListPagination
            page={page}
            pageSize={pageSize}
            totalCount={logs.data?.totalCount ?? 0}
            totalPages={logs.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => update({ pageSize: size }, false)}
          />
        </>
      )}
    </>
  )
}
