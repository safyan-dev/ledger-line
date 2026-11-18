import { useQuery } from '@tanstack/react-query'
import { Building2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { ListPagination } from '@/components/data/list-pagination'
import { SearchInput } from '@/components/data/search-input'
import { StatusBadge } from '@/components/data/status-badge'
import { PageHeader } from '@/components/layout/page-header'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { getPlatformTenants } from '@/features/platform/api'
import { errorMessage } from '@/lib/api-client'
import { formatDate } from '@/lib/format'
import { useTableParams } from '@/lib/use-table-params'
import type { PlatformTenantDto } from '@/types/api'

const columns: DataTableColumn<PlatformTenantDto>[] = [
  {
    id: 'name',
    header: 'Tenant',
    cell: (tenant) => (
      <div className="min-w-0">
        <Link
          to={`/platform/tenants/${tenant.id}`}
          className="truncate text-sm font-medium text-foreground hover:text-primary"
        >
          {tenant.name}
        </Link>
        <p className="mt-0.5 font-mono text-xs text-muted-foreground">{tenant.slug}</p>
      </div>
    ),
  },
  {
    id: 'owner',
    header: 'Owner email',
    cell: (tenant) => (
      <span className="text-sm text-foreground">{tenant.ownerEmail ?? 'Not available'}</span>
    ),
  },
  {
    id: 'plan',
    header: 'Plan',
    cell: (tenant) => <span className="text-sm text-foreground">{tenant.planName ?? 'No subscription'}</span>,
  },
  {
    id: 'status',
    header: 'Status',
    cell: (tenant) => (
      <StatusBadge tone={tenant.status === 'Active' ? 'success' : tenant.status === 'Suspended' ? 'danger' : 'neutral'}>
        {tenant.status}
      </StatusBadge>
    ),
  },
  {
    id: 'members',
    header: 'Members',
    className: 'hidden text-right md:table-cell',
    cell: (tenant) => <span className="text-sm tabular-nums text-foreground">{tenant.memberCount}</span>,
  },
  {
    id: 'created',
    header: 'Created',
    className: 'hidden text-right lg:table-cell',
    cell: (tenant) => <span className="text-xs text-muted-foreground">{formatDate(tenant.createdAt)}</span>,
  },
]

export function PlatformPage() {
  const table = useTableParams(25)
  const search = table.get('search')
  const status = table.get('status')
  const tenants = useQuery({
    queryKey: ['platform', 'tenants', table.page, table.pageSize, search, status],
    queryFn: () =>
      getPlatformTenants({
        page: table.page,
        pageSize: table.pageSize,
        search: search || undefined,
        status: status || undefined,
      }),
  })
  const pageItems = tenants.data?.items ?? []
  const totalCount = tenants.data?.totalCount ?? 0
  const totalPages = tenants.data?.totalPages ?? 0

  return (
    <div>
      <PageHeader title="Tenants" description="Find and manage customer workspaces across Ledgerline." />

      <FilterBar>
        <SearchInput
          defaultValue={table.get('search')}
          onCommit={(value) => table.update({ search: value })}
          placeholder="Search tenants or owners…"
        />
        <Select value={status || 'All'} onValueChange={(value) => table.update({ status: value === 'All' ? '' : value })}>
          <SelectTrigger className="w-full sm:w-44" aria-label="Filter tenant status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All statuses</SelectItem>
            <SelectItem value="Active">Active</SelectItem>
            <SelectItem value="Suspended">Suspended</SelectItem>
            <SelectItem value="Canceled">Canceled</SelectItem>
          </SelectContent>
        </Select>
      </FilterBar>

      {tenants.isError ? (
        <ErrorState message={errorMessage(tenants.error)} onRetry={() => void tenants.refetch()} />
      ) : tenants.isLoading || !tenants.data ? (
        <DataTable
          columns={columns}
          rows={[]}
          getRowKey={(tenant) => tenant.id}
          caption="Platform tenants"
          isLoading
          empty={null}
        />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={pageItems}
            getRowKey={(tenant) => tenant.id}
            caption="Platform tenants"
            empty={
              <EmptyState
                icon={<Building2 className="size-4" aria-hidden />}
                title={totalCount === 0 ? 'No tenants yet' : 'No tenants match these filters'}
                description={
                  totalCount === 0
                    ? 'New customer workspaces will appear here.'
                    : 'Try a different search or status filter.'
                }
              />
            }
          />
          {totalCount > 0 ? (
            <ListPagination
              page={tenants.data?.page ?? 1}
              pageSize={table.pageSize}
              totalCount={totalCount}
              totalPages={totalPages}
              onPageChange={table.setPage}
              onPageSizeChange={table.setPageSize}
              className="px-1"
            />
          ) : null}
        </>
      )}
    </div>
  )
}
