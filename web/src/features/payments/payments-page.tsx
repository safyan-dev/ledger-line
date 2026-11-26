import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CreditCard, X } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { FilterReset } from '@/components/data/filter-reset'
import { ListPagination } from '@/components/data/list-pagination'
import { Money } from '@/components/data/money'
import { CopyableId } from '@/components/data/copy-button'
import { SearchInput } from '@/components/data/search-input'
import { StatusBadge } from '@/components/data/status-badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { RecordPaymentDialog } from '@/features/payments/components/record-payment-dialog'
import { getPayments } from '@/features/payments/api'
import { formatDateTime } from '@/lib/format'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'
import { paymentStatusMeta } from '@/lib/status'
import { useTableParams } from '@/lib/use-table-params'
import type { PaymentListItemDto } from '@/types/api'
import { errorStateProps } from '@/lib/api-client'

const columns: DataTableColumn<PaymentListItemDto>[] = [
  {
    id: 'payment',
    header: 'Provider transaction',
    cell: (payment) => (
      <div className="min-w-0 max-w-[130px] sm:max-w-[260px]">
        <CopyableId value={payment.providerPaymentId} label="provider payment id" />
        <p className="mt-0.5 truncate text-[11px] text-muted-foreground">
          {payment.customerName ?? 'Unknown customer'}
        </p>
        {payment.orderId ? (
          <Link
            to={`/orders/${payment.orderId}`}
            onClick={(event) => event.stopPropagation()}
            className="mt-1 block truncate text-[11px] text-primary hover:underline md:hidden"
          >
            {payment.orderNumber ?? 'Linked order'}
          </Link>
        ) : (
          <span className="mt-1 block text-[11px] text-muted-foreground md:hidden">Unlinked</span>
        )}
      </div>
    ),
  },
  {
    id: 'order',
    header: 'Order',
    className: 'hidden md:table-cell',
    cell: (payment) =>
      payment.orderId ? (
        <Link
          to={`/orders/${payment.orderId}`}
          onClick={(event) => event.stopPropagation()}
          className="text-sm text-primary hover:underline"
        >
          {payment.orderNumber ?? 'Linked order'}
        </Link>
      ) : (
        <span className="text-xs text-muted-foreground">Unlinked</span>
      ),
  },
  {
    id: 'amount',
    header: 'Amount',
    className: 'text-right',
    cell: (payment) => <Money amount={payment.amount} currency={payment.currency} />,
  },
  {
    id: 'currency',
    header: 'Currency',
    className: 'hidden text-xs text-muted-foreground md:table-cell',
    cell: (payment) => payment.currency,
  },
  {
    id: 'provider',
    header: 'Provider',
    className: 'hidden md:table-cell',
    cell: (payment) => <span className="text-xs capitalize text-muted-foreground">{payment.provider}</span>,
  },
  {
    id: 'reference',
    header: 'Reference',
    className: 'hidden xl:table-cell',
    cell: (payment) => (
      <span className="text-xs text-muted-foreground">{payment.reference ?? '—'}</span>
    ),
  },
  {
    id: 'status',
    header: 'Status',
    cell: (payment) => (
      <StatusBadge tone={paymentStatusMeta(payment.status).tone}>{paymentStatusMeta(payment.status).label}</StatusBadge>
    ),
  },
  {
    id: 'received',
    header: 'Received',
    className: 'hidden whitespace-nowrap text-right lg:table-cell',
    cell: (payment) => <span className="text-xs text-muted-foreground">{formatDateTime(payment.receivedAt)}</span>,
  },
]

export function PaymentsPage() {
  const navigate = useNavigate()
  const tenantId = useTenantKey()
  const { page, pageSize, get, update, setPage } = useTableParams()
  const status = get('status')
  const provider = get('provider')
  const search = get('search')
  const orderId = get('orderId')

  const payments = useQuery({
    queryKey: qk.payments(tenantId, { page, pageSize, status, provider, search, orderId }),
    queryFn: () =>
      getPayments({
        page,
        pageSize,
        status: status || undefined,
        provider: provider || undefined,
        search: search || undefined,
        orderId: orderId || undefined,
      }),
  })

  return (
    <>
      <PageHeader
        title="Payments"
        description="Every recorded attempt, what it was matched to, and what still needs a decision."
        actions={<RecordPaymentDialog />}
      />

      <FilterBar>
        <SearchInput
          key={search}
          defaultValue={search}
          onCommit={(value) => update({ search: value || undefined })}
          placeholder="Search payments"
        />
        <Select value={status || 'all'} onValueChange={(value) => update({ status: value === 'all' ? undefined : value })}>
          <SelectTrigger className="h-9 w-[160px]" aria-label="Filter by payment status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="Received">Received</SelectItem>
            <SelectItem value="Matched">Matched</SelectItem>
            <SelectItem value="Unmatched">Unmatched</SelectItem>
            <SelectItem value="PartiallyRefunded">Partially refunded</SelectItem>
            <SelectItem value="Refunded">Refunded</SelectItem>
            <SelectItem value="Failed">Failed</SelectItem>
          </SelectContent>
        </Select>
        <Select
          value={provider || 'all'}
          onValueChange={(value) => update({ provider: value === 'all' ? undefined : value })}
        >
          <SelectTrigger className="h-9 w-[150px]" aria-label="Filter by provider">
            <SelectValue placeholder="All providers" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All providers</SelectItem>
            <SelectItem value="mock">Mock</SelectItem>
            <SelectItem value="stripe">Stripe</SelectItem>
            <SelectItem value="manual">Manual</SelectItem>
          </SelectContent>
        </Select>
        {orderId ? (
          <span className="inline-flex h-9 items-center gap-2 rounded-md border bg-muted/40 px-2.5 text-xs text-muted-foreground">
            Filtered by order
            <Link to="/payments" className="text-foreground hover:text-primary" aria-label="Clear order filter">
              <X className="size-3.5" aria-hidden />
            </Link>
          </span>
        ) : null}
        <FilterReset
          visible={Boolean(status || provider || search || orderId)}
          onClick={() => update({ status: undefined, provider: undefined, search: undefined, orderId: undefined })}
        />
      </FilterBar>

      {payments.isError ? (
        <ErrorState {...errorStateProps(payments.error)} onRetry={() => void payments.refetch()} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={payments.data?.items ?? []}
            getRowKey={(payment) => payment.id}
            caption="Recorded payments and their reconciliation status"
            isLoading={payments.isLoading}
            onRowClick={(payment) => navigate(`/payments/${payment.id}`)}
            empty={
              <EmptyState
                icon={<CreditCard className="size-4" aria-hidden />}
                title="No payments match these filters"
                description="Clear the filters or record a payment to start a reconciliation attempt."
                action={<RecordPaymentDialog />}
              />
            }
          />
          <ListPagination
            page={page}
            pageSize={pageSize}
            totalCount={payments.data?.totalCount ?? 0}
            totalPages={payments.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => update({ pageSize: size }, false)}
          />
        </>
      )}
    </>
  )
}
