import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ShoppingCart } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { FilterReset } from '@/components/data/filter-reset'
import { CopyableId } from '@/components/data/copy-button'
import { ListPagination } from '@/components/data/list-pagination'
import { Money } from '@/components/data/money'
import { SearchInput } from '@/components/data/search-input'
import { StatusBadge } from '@/components/data/status-badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { CreateOrderDialog } from '@/features/orders/components/create-order-dialog'
import { getOrders } from '@/features/orders/api'
import { formatDate } from '@/lib/format'
import { orderStatusMeta } from '@/lib/status'
import { useTableParams } from '@/lib/use-table-params'
import type { OrderListItemDto } from '@/types/api'

const columns: DataTableColumn<OrderListItemDto>[] = [
  {
    id: 'order',
    header: 'Order reference',
    cell: (order) => (
      <div className="min-w-0">
        <CopyableId value={order.orderNumber} label="order number" />
        <p className="mt-0.5 truncate text-[11px] text-muted-foreground md:hidden">
          {order.customerReference ?? 'No customer reference'}
        </p>
        <p className="mt-1 text-xs font-medium md:hidden">
          <Money amount={order.expectedAmount} currency={order.currency} />
        </p>
      </div>
    ),
  },
  {
    id: 'customerReference',
    header: 'Customer reference',
    className: 'hidden md:table-cell',
    cell: (order) => (
      <span className="text-xs text-muted-foreground">{order.customerReference ?? '—'}</span>
    ),
  },
  {
    id: 'customer',
    header: 'Customer',
    className: 'hidden md:table-cell',
    cell: (order) => <span className="text-foreground">{order.customerName}</span>,
  },
  {
    id: 'expected',
    header: 'Expected',
    className: 'hidden text-right md:table-cell',
    cell: (order) => <Money amount={order.expectedAmount} currency={order.currency} />,
  },
  {
    id: 'currency',
    header: 'Currency',
    className: 'hidden text-xs text-muted-foreground lg:table-cell',
    cell: (order) => order.currency,
  },
  {
    id: 'paid',
    header: 'Paid',
    className: 'hidden text-right xl:table-cell',
    cell: (order) => (
      <Money
        amount={order.paidAmount}
        currency={order.currency}
        className={order.paidAmount > order.expectedAmount ? 'font-medium text-warning-foreground' : undefined}
      />
    ),
  },
  {
    id: 'status',
    header: 'Status',
    cell: (order) => (
      <StatusBadge tone={orderStatusMeta(order.status).tone}>{orderStatusMeta(order.status).label}</StatusBadge>
    ),
  },
  {
    id: 'created',
    header: 'Created',
    className: 'hidden whitespace-nowrap text-right lg:table-cell',
    cell: (order) => <span className="text-xs text-muted-foreground">{formatDate(order.createdAt)}</span>,
  },
  {
    id: 'actions',
    header: '',
    className: 'hidden w-16 text-right sm:table-cell',
    cell: (order) => (
      <Link
        to={`/orders/${order.id}`}
        onClick={(event) => event.stopPropagation()}
        className="text-xs font-medium text-primary hover:underline"
      >
        View
      </Link>
    ),
  },
]

export function OrdersPage() {
  const navigate = useNavigate()
  const { page, pageSize, get, update, setPage } = useTableParams()
  const status = get('status')
  const search = get('search')
  const sort = get('sort')
  const [createOpen, setCreateOpen] = useState(
    () => new URLSearchParams(window.location.search).get('new') === '1',
  )

  const orders = useQuery({
    queryKey: ['orders', { page, pageSize, status, search, sort }],
    queryFn: () =>
      getOrders({
        page,
        pageSize,
        status: status || undefined,
        search: search || undefined,
        sort: sort || undefined,
      }),
  })

  return (
    <>
      <PageHeader
        title="Orders"
        description="Expected amounts, allocations and the status of each customer order."
        actions={<CreateOrderDialog open={createOpen} onOpenChange={setCreateOpen} />}
      />

      <FilterBar>
        <SearchInput
          key={search}
          defaultValue={search}
          onCommit={(value) => update({ search: value || undefined })}
          placeholder="Search orders"
        />
        <Select
          value={status || 'all'}
          onValueChange={(value) => update({ status: value === 'all' ? undefined : value })}
        >
          <SelectTrigger className="h-9 w-[170px]" aria-label="Filter by status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="Pending">Pending</SelectItem>
            <SelectItem value="PartiallyPaid">Partially paid</SelectItem>
            <SelectItem value="Paid">Paid</SelectItem>
            <SelectItem value="Overpaid">Overpaid</SelectItem>
            <SelectItem value="Cancelled">Cancelled</SelectItem>
          </SelectContent>
        </Select>
        <Select value={sort || 'created_desc'} onValueChange={(value) => update({ sort: value === 'created_desc' ? undefined : value })}>
          <SelectTrigger className="h-9 w-[170px]" aria-label="Sort orders">
            <SelectValue placeholder="Newest first" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="created_desc">Newest first</SelectItem>
            <SelectItem value="created_asc">Oldest first</SelectItem>
            <SelectItem value="amount_desc">Amount high to low</SelectItem>
            <SelectItem value="amount_asc">Amount low to high</SelectItem>
            <SelectItem value="order_asc">Order A–Z</SelectItem>
            <SelectItem value="order_desc">Order Z–A</SelectItem>
          </SelectContent>
        </Select>
        <FilterReset
          visible={Boolean(status || search || sort)}
          onClick={() => update({ status: undefined, search: undefined, sort: undefined })}
        />
      </FilterBar>

      {orders.isError ? (
        <ErrorState message={orders.error.message} onRetry={() => void orders.refetch()} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={orders.data?.items ?? []}
            getRowKey={(order) => order.id}
            caption="Customer orders with expected and paid amounts"
            isLoading={orders.isLoading}
            onRowClick={(order) => navigate(`/orders/${order.id}`)}
            empty={
              <EmptyState
                icon={<ShoppingCart className="size-4" aria-hidden />}
                title="No orders yet"
                description="Create an order to give incoming payments something to reconcile against."
                action={<CreateOrderDialog />}
              />
            }
          />
          <ListPagination
            page={page}
            pageSize={pageSize}
            totalCount={orders.data?.totalCount ?? 0}
            totalPages={orders.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => update({ pageSize: size }, false)}
          />
        </>
      )}
    </>
  )
}
