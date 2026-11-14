import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowRight, BarChart3, CircleAlert, CircleCheck, CreditCard, ShoppingCart, TriangleAlert } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { ErrorState } from '@/components/data/error-state'
import { StatCard } from '@/components/data/stat-card'
import { getDashboardSummary } from '@/features/dashboard/api'
import { errorMessage } from '@/lib/api-client'
import { formatAmount, formatDate, formatPercent } from '@/lib/format'
import type { VolumePoint } from '@/types/api'

const volumeColumns: DataTableColumn<VolumePoint>[] = [
  {
    id: 'date',
    header: 'Date',
    cell: (point) => <span className="text-sm text-foreground">{formatDate(point.date)}</span>,
  },
  {
    id: 'matched',
    header: 'Matched',
    className: 'text-right',
    cell: (point) => <span className="text-sm tabular-nums text-foreground">{formatAmount(point.matchedAmount)}</span>,
  },
  {
    id: 'unmatched',
    header: 'Unmatched',
    className: 'text-right',
    cell: (point) => <span className="text-sm tabular-nums text-warning-foreground">{formatAmount(point.unmatchedAmount)}</span>,
  },
  {
    id: 'manual',
    header: 'Manual',
    className: 'text-right',
    cell: (point) => <span className="text-sm tabular-nums text-foreground">{formatAmount(point.manualAmount)}</span>,
  },
]

export function ReportsPage() {
  const summary = useQuery({ queryKey: ['dashboard'], queryFn: getDashboardSummary })

  if (summary.isError) {
    return (
      <>
        <PageHeader title="Reports" description="Financial and reconciliation reporting from live workspace data." />
        <ErrorState message={errorMessage(summary.error)} onRetry={() => void summary.refetch()} />
      </>
    )
  }

  if (summary.isLoading || !summary.data) {
    return (
      <>
        <PageHeader title="Reports" description="Financial and reconciliation reporting from live workspace data." />
        <div className="space-y-4" aria-busy="true" aria-label="Loading reports">
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            {Array.from({ length: 5 }).map((_, index) => (
              <Skeleton key={index} className="h-32 w-full rounded-xl" />
            ))}
          </div>
          <Skeleton className="h-72 w-full rounded-xl" />
        </div>
      </>
    )
  }

  const data = summary.data

  return (
    <>
      <PageHeader
        title="Reports"
        description="Financial and reconciliation reporting computed from the live workspace data."
        actions={
          <Button asChild variant="outline">
            <Link to="/reconciliation">
              Open reconciliation
              <ArrowRight className="size-4" aria-hidden />
            </Link>
          </Button>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard label="Total orders" value={data.flow.ordersCreated} hint="All statuses" />
        <StatCard label="Successful payments" value={data.flow.successfulPayments} hint="Excludes failed attempts" />
        <StatCard label="Matched rate · 7d" value={formatPercent(data.kpis.matchedRate7d)} hint="Reconciliation coverage" />
        <StatCard
          label="Unmatched value"
          value={formatAmount(data.kpis.unmatchedAmount)}
          hint="Cross-currency raw total"
          delta={data.flow.unmatched > 0 ? { value: `${data.flow.unmatched} open`, tone: 'negative' } : { value: 'Clear', tone: 'positive' }}
        />
        <StatCard label="Manual matches" value={data.flow.manualMatches} hint="Operator-resolved payments" />
      </div>

      <div className="mt-4 grid gap-4 xl:grid-cols-[minmax(0,1.4fr)_minmax(300px,0.6fr)]">
        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <BarChart3 className="size-4 text-primary" aria-hidden />
              Payment volume
            </CardTitle>
            <CardDescription>Matched, unmatched and manual amounts recorded by the backend.</CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            {data.volume.length === 0 ? (
              <p className="px-6 py-8 text-sm text-muted-foreground">No volume data recorded yet.</p>
            ) : (
              <DataTable
                columns={volumeColumns}
                rows={data.volume}
                getRowKey={(point) => point.date}
                caption="Payment volume by date"
              />
            )}
          </CardContent>
        </Card>

        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader>
            <CardTitle className="text-base">Reconciliation health</CardTitle>
            <CardDescription>Where the workspace needs attention.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <HealthRow icon={<CircleCheck className="size-4 text-success" aria-hidden />} label="Matched payments" value={data.flow.matched} />
            <HealthRow icon={<TriangleAlert className="size-4 text-warning-foreground" aria-hidden />} label="Unmatched payments" value={data.flow.unmatched} />
            <HealthRow icon={<CircleAlert className="size-4 text-destructive" aria-hidden />} label="Pending review" value={data.kpis.pendingReview} />
            <HealthRow icon={<CreditCard className="size-4 text-primary" aria-hidden />} label="Payments today" value={data.kpis.paymentsToday} />
            <HealthRow icon={<ShoppingCart className="size-4 text-primary" aria-hidden />} label="Webhooks received" value={data.flow.webhooksReceived} />

            <div className="flex flex-wrap gap-2 border-t border-border/60 pt-3">
              <Button asChild variant="outline" size="sm">
                <Link to="/payments?status=Unmatched">Review unmatched</Link>
              </Button>
              <Button asChild variant="ghost" size="sm">
                <Link to="/audit">Audit history</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </>
  )
}

function HealthRow({ icon, label, value }: { icon: ReactNode; label: string; value: number }) {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-border/60 pb-2 last:border-0 last:pb-0">
      <span className="flex items-center gap-2 text-xs text-muted-foreground">
        {icon}
        {label}
      </span>
      <span className="text-sm font-semibold tabular-nums text-foreground">{value}</span>
    </div>
  )
}
