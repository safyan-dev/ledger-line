import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { GitCompareArrows, Link2, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { PageHeader } from '@/components/layout/page-header'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { FilterBar } from '@/components/data/filter-bar'
import { FilterReset } from '@/components/data/filter-reset'
import { ListPagination } from '@/components/data/list-pagination'
import { ManualMatchDialog } from '@/components/data/manual-match-dialog'
import { Money } from '@/components/data/money'
import { SearchInput } from '@/components/data/search-input'
import { StatusBadge } from '@/components/data/status-badge'
import { DatePicker } from '@/components/data/date-picker'
import { FailedPayments } from '@/features/reconciliation/components/failed-payments'
import { getReconciliationRecords } from '@/features/reconciliation/api'
import { formatRelative } from '@/lib/format'
import { reconciliationOutcomeMeta, strategyLabel } from '@/lib/status'
import { useTableParams } from '@/lib/use-table-params'
import type { ReconciliationRecordDto } from '@/types/api'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'
import { errorStateProps } from '@/lib/api-client'
import { parseDateParam } from '@/lib/date-param'

const tabs = [
  { value: 'all', label: 'All' },
  { value: 'Matched', label: 'Matched' },
  { value: 'PartiallyMatched', label: 'Partial' },
  { value: 'AmountMismatch', label: 'Amount mismatch' },
  { value: 'Unmatched', label: 'Unmatched' },
  { value: 'Overpaid', label: 'Overpaid' },
  { value: 'Ambiguous', label: 'Ambiguous' },
  { value: 'ManuallyMatched', label: 'Manual' },
]

function paymentLink(record: ReconciliationRecordDto) {
  return (
    <Link
      to={`/payments/${record.paymentId}`}
      className="font-mono text-xs font-medium text-foreground hover:text-primary hover:underline"
    >
      {record.providerPaymentId ?? record.paymentId}
    </Link>
  )
}

const columns: DataTableColumn<ReconciliationRecordDto>[] = [
  {
    id: 'payment',
    header: 'Payment',
    cell: (record) => (
      <div className="min-w-0 max-w-[200px] sm:max-w-[310px]">
        {paymentLink(record)}
        <p className="mt-1 line-clamp-2 text-[11px] leading-4 text-muted-foreground" title={record.reason ?? undefined}>
          {record.reason ?? 'No reason recorded.'}
        </p>
        <p className="mt-1 text-xs font-medium tabular-nums md:hidden">
          Actual <Money amount={record.actualAmount} currency={record.currency} />
        </p>
      </div>
    ),
  },
  {
    id: 'order',
    header: 'Order',
    className: 'hidden lg:table-cell',
    cell: (record) =>
      record.orderId ? (
        <Link to={`/orders/${record.orderId}`} className="text-sm text-primary hover:underline">
          {record.orderNumber ?? 'Linked order'}
        </Link>
      ) : (
        <span className="text-xs text-muted-foreground">Not linked</span>
      ),
  },
  {
    id: 'expected',
    header: 'Expected',
    className: 'hidden text-right md:table-cell',
    cell: (record) =>
      record.expectedAmount === null ? (
        <span className="text-xs text-muted-foreground">—</span>
      ) : (
        <Money amount={record.expectedAmount} currency={record.currency} />
      ),
  },
  {
    id: 'actual',
    header: 'Actual',
    className: 'hidden text-right md:table-cell',
    cell: (record) => <Money amount={record.actualAmount} currency={record.currency} />,
  },
  {
    id: 'difference',
    header: 'Difference',
    className: 'hidden text-right lg:table-cell',
    cell: (record) => (
      <Money
        amount={record.difference}
        currency={record.currency}
        className={
          record.difference < 0
            ? 'font-medium text-warning-foreground'
            : record.difference > 0
              ? 'font-medium text-primary'
              : undefined
        }
      />
    ),
  },
  {
    id: 'outcome',
    header: 'Result',
    cell: (record) => (
      <StatusBadge tone={reconciliationOutcomeMeta(record.outcome).tone}>
        {reconciliationOutcomeMeta(record.outcome).label}
      </StatusBadge>
    ),
  },
  {
    id: 'strategy',
    header: 'Rule',
    className: 'hidden lg:table-cell',
    cell: (record) => <span className="text-xs text-muted-foreground">{strategyLabel(record.strategy)}</span>,
  },
  {
    id: 'when',
    header: 'When',
    className: 'hidden whitespace-nowrap text-right md:table-cell',
    cell: (record) => <span className="text-xs text-muted-foreground">{formatRelative(record.createdAt)}</span>,
  },
]

export function ReconciliationPage() {
  const tenantId = useTenantKey()
  const { page, pageSize, get, update, setPage } = useTableParams()
  const outcome = get('outcome')
  const search = get('search')
  const from = get('from')
  const to = get('to')
  const reason = get('reason')
  const orderId = get('orderId')
  const [matchOpen, setMatchOpen] = useState(false)
  const currencyOnly = reason === 'Currency mismatch'

  const records = useQuery({
    queryKey: qk.reconciliation(tenantId, { page, pageSize, outcome, search, from, to, reason, orderId }),
    queryFn: () =>
      getReconciliationRecords({
        page,
        pageSize,
        outcome: outcome || undefined,
        search: search || undefined,
        from: from || undefined,
        to: to || undefined,
        reason: reason || undefined,
        orderId: orderId || undefined,
      }),
  })

  const hasFilters = Boolean(outcome || search || from || to || reason || orderId)

  return (
    <>
      <PageHeader
        title="Reconciliation"
        description="Investigate why payments matched, or why they need attention."
        actions={
          <Button onClick={() => setMatchOpen(true)}>
            <Link2 className="size-4" aria-hidden />
            Match payment
          </Button>
        }
      />

      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <Tabs
          value={outcome || 'all'}
          onValueChange={(value) => update({ outcome: value === 'all' ? undefined : value, reason: undefined })}
        >
          <TabsList className="h-9 flex-wrap">
            {tabs.map((tab) => (
              <TabsTrigger key={tab.value} value={tab.value} className="text-xs">
                {tab.label}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        {orderId ? (
          <span className="inline-flex h-9 items-center gap-2 rounded-md border bg-muted/40 px-2.5 text-xs text-muted-foreground">
            Filtered by order
            <button
              type="button"
              onClick={() => update({ orderId: undefined })}
              className="text-foreground hover:text-primary"
              aria-label="Clear order filter"
            >
              <X className="size-3.5" aria-hidden />
            </button>
          </span>
        ) : null}
      </div>

      <FilterBar>
        <SearchInput
          key={search}
          defaultValue={search}
          onCommit={(value) => update({ search: value || undefined })}
          placeholder="Search payment, order or reason"
        />
        <div className="flex items-center gap-1.5">
          <span className="text-[11px] font-medium text-muted-foreground">From</span>
          <DatePicker
            value={from}
            onChange={(value) => update({ from: value })}
            placeholder="From date"
            ariaLabel="Reconciliation from date"
            toDate={parseDateParam(to)}
          />
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[11px] font-medium text-muted-foreground">To</span>
          <DatePicker
            value={to}
            onChange={(value) => update({ to: value })}
            placeholder="To date"
            ariaLabel="Reconciliation to date"
            fromDate={parseDateParam(from)}
          />
        </div>
        <Button
          type="button"
          variant={currencyOnly ? 'default' : 'outline'}
          size="sm"
          className="h-9"
          onClick={() =>
            update(
              currencyOnly
                ? { outcome: undefined, reason: undefined }
                : { outcome: 'Unmatched', reason: 'Currency mismatch' },
            )
          }
        >
          Currency mismatch
        </Button>
        <FilterReset
          visible={hasFilters}
          onClick={() =>
            update({
              outcome: undefined,
              search: undefined,
              from: undefined,
              to: undefined,
              reason: undefined,
              orderId: undefined,
            })
          }
        />
      </FilterBar>

      {currencyOnly ? (
        <p className="mb-3 rounded-lg border border-warning/30 bg-warning/10 px-3 py-2 text-xs text-warning-foreground">
          Showing unmatched reconciliation records with a currency-mismatch reason.
        </p>
      ) : null}

      {records.isError ? (
        <ErrorState {...errorStateProps(records.error)} onRetry={() => void records.refetch()} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={records.data?.items ?? []}
            getRowKey={(record) => record.id}
            caption="Reconciliation attempts with their outcomes"
            isLoading={records.isLoading}
            empty={
              <EmptyState
                icon={<GitCompareArrows className="size-4" aria-hidden />}
                title={hasFilters ? 'No attempts match these filters' : 'No reconciliation attempts'}
                description={
                  hasFilters
                    ? 'Clear one or more filters to widen the search.'
                    : 'Attempts are created automatically when a payment is recorded, or manually from the review queue.'
                }
                action={
                  <Button variant="outline" size="sm" onClick={() => setMatchOpen(true)}>
                    <Link2 className="size-3.5" aria-hidden />
                    Match a payment
                  </Button>
                }
              />
            }
          />
          <ListPagination
            page={page}
            pageSize={pageSize}
            totalCount={records.data?.totalCount ?? 0}
            totalPages={records.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => update({ pageSize: size }, false)}
          />
        </>
      )}

      <FailedPayments />

      <ManualMatchDialog open={matchOpen} onOpenChange={setMatchOpen} />
    </>
  )
}
