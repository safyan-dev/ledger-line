import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  ArrowRight,
  CircleAlert,
  CircleCheck,
  CreditCard,
  Plus,
  ShoppingCart,
  TriangleAlert,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { PageHeader } from '@/components/layout/page-header'
import { ErrorState } from '@/components/data/error-state'
import { StatCard } from '@/components/data/stat-card'
import { ManualMatchDialog } from '@/components/data/manual-match-dialog'
import { RefundDialog } from '@/components/data/refund-dialog'
import { RecentPayments } from '@/features/dashboard/components/recent-payments'
import { RecentReconciliation } from '@/features/dashboard/components/recent-reconciliation'
import { ReviewQueue } from '@/features/dashboard/components/review-queue'
import { getDashboardSummary } from '@/features/dashboard/api'
import { getReconciliationRecords } from '@/features/reconciliation/api'
import { errorMessage } from '@/lib/api-client'
import { formatAmount, formatPercent } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { DashboardSummary, ReviewQueueItem } from '@/types/api'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'

type ReviewQueueAction =
  | { type: 'match'; item: ReviewQueueItem }
  | { type: 'refund'; item: ReviewQueueItem }
  | null

export function DashboardPage() {
  const tenantId = useTenantKey()
  const [activeAction, setActiveAction] = useState<ReviewQueueAction>(null)

  const summary = useQuery({
    queryKey: qk.dashboard(tenantId),
    queryFn: getDashboardSummary,
    // The summary is live: always refetch on mount so a mutation elsewhere is
    // reflected when the user lands here.
    staleTime: 0,
  })
  const amountMismatches = useQuery({
    queryKey: qk.reconciliation(tenantId, { scope: 'amount-mismatch' }),
    queryFn: () => getReconciliationRecords({ page: 1, pageSize: 1, outcome: 'AmountMismatch' }),
  })

  const header = (
    <PageHeader
      title="Reconciliation dashboard"
      description="Payment operations at a glance, with exceptions surfaced first."
      actions={
        <>
          <Button variant="outline" asChild>
            <Link to="/payments">
              Review payments
              <ArrowRight className="size-4" aria-hidden />
            </Link>
          </Button>
          <Button asChild>
            <Link to="/orders?new=1">
              <Plus className="size-4" aria-hidden />
              New order
            </Link>
          </Button>
        </>
      }
    />
  )

  if (summary.isError) {
    return (
      <>
        {header}
        <ErrorState message={errorMessage(summary.error)} onRetry={() => void summary.refetch()} />
      </>
    )
  }

  if (summary.isLoading || !summary.data) {
    return (
      <>
        {header}
        <DashboardSkeleton />
      </>
    )
  }

  const data = summary.data
  const mismatchCount = amountMismatches.isLoading ? null : (amountMismatches.data?.totalCount ?? 0)

  return (
    <>
      {header}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard label="Total orders" value={data.flow.ordersCreated} hint="Across all statuses">
          <KpiIcon tone="primary">
            <ShoppingCart className="size-4" aria-hidden />
          </KpiIcon>
        </StatCard>
        <StatCard label="Successful payments" value={data.flow.successfulPayments} hint="Excludes failed attempts">
          <KpiIcon tone="success">
            <CreditCard className="size-4" aria-hidden />
          </KpiIcon>
        </StatCard>
        <StatCard label="Matched payments" value={data.flow.matched} hint="Currently matched to orders">
          <KpiIcon tone="success">
            <CircleCheck className="size-4" aria-hidden />
          </KpiIcon>
        </StatCard>
        <StatCard
          label="Unmatched payments"
          value={data.flow.unmatched}
          hint="Awaiting operator action"
          delta={data.flow.unmatched > 0 ? { value: 'Needs review', tone: 'negative' } : { value: 'Clear', tone: 'positive' }}
        >
          <KpiIcon tone="warning">
            <TriangleAlert className="size-4" aria-hidden />
          </KpiIcon>
        </StatCard>
        <StatCard
          label="Amount mismatches"
          value={mismatchCount ?? '—'}
          hint="Payments whose amount differs from the order"
          delta={mismatchCount ? { value: 'Review', tone: 'negative' } : undefined}
        >
          <KpiIcon tone="danger">
            <CircleAlert className="size-4" aria-hidden />
          </KpiIcon>
        </StatCard>
      </div>

      <div className="mt-4 grid gap-4 xl:grid-cols-[minmax(0,1.35fr)_minmax(300px,0.65fr)]">
        <ReviewQueue
          items={data.reviewQueue}
          onMatch={(item) => setActiveAction({ type: 'match', item })}
          onRefund={(item) => setActiveAction({ type: 'refund', item })}
        />
        <OperationalHealth data={data} mismatchCount={mismatchCount} />
      </div>

      <div className="mt-4 grid gap-4 xl:grid-cols-2">
        <RecentPayments />
        <RecentReconciliation />
      </div>

      <ManualMatchDialog
        open={activeAction?.type === 'match'}
        onOpenChange={(open) => {
          if (!open) setActiveAction((current) => current?.type === 'match' ? null : current)
        }}
        paymentId={activeAction?.type === 'match' ? activeAction.item.paymentId : undefined}
        paymentLabel={
          activeAction?.type === 'match'
            ? `${activeAction.item.providerPaymentId} · ${formatMoneyLabel(activeAction.item)}`
            : undefined
        }
      />

      <RefundDialog
        open={activeAction?.type === 'refund'}
        onOpenChange={(open) => {
          if (!open) setActiveAction((current) => current?.type === 'refund' ? null : current)
        }}
        paymentId={activeAction?.type === 'refund' ? activeAction.item.paymentId : undefined}
        maxAmount={activeAction?.type === 'refund' ? activeAction.item.amount : 0}
        currency={activeAction?.type === 'refund' ? activeAction.item.currency : undefined}
        paymentLabel={
          activeAction?.type === 'refund'
            ? `${activeAction.item.providerPaymentId} · ${formatMoneyLabel(activeAction.item)}`
            : undefined
        }
      />
    </>
  )
}

function OperationalHealth({ data, mismatchCount }: { data: DashboardSummary; mismatchCount: number | null }) {
  return (
    <Card className="min-w-0 border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Operational health</CardTitle>
        <CardDescription>Matched coverage and the value still waiting on a decision.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-0">
        <HealthRow label="Matched rate · 7d" value={formatPercent(data.kpis.matchedRate7d)} />
        <HealthRow
          label="Unmatched value (raw)"
          value={<span title="Cross-currency total; review each payment's currency.">{formatAmount(data.kpis.unmatchedAmount)}</span>}
        />
        <HealthRow label="Payments today" value={data.kpis.paymentsToday} />
        <HealthRow label="Manual matches" value={data.flow.manualMatches} />
        <HealthRow label="Amount mismatches" value={mismatchCount ?? '—'} />
      </CardContent>
    </Card>
  )
}

function HealthRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-border/60 py-2.5 last:border-0">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-semibold tabular-nums text-foreground">{value}</span>
    </div>
  )
}

function KpiIcon({ tone, children }: { tone: 'primary' | 'success' | 'warning' | 'danger'; children: ReactNode }) {
  return (
    <div
      className={cn(
        'flex size-9 items-center justify-center rounded-lg',
        tone === 'primary'
          ? 'bg-primary/10 text-primary'
          : tone === 'success'
            ? 'bg-success/10 text-success'
            : tone === 'warning'
              ? 'bg-warning/15 text-warning-foreground'
              : 'bg-destructive/10 text-destructive',
      )}
    >
      {children}
    </div>
  )
}

function DashboardSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true" aria-label="Loading dashboard">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {Array.from({ length: 5 }).map((_, index) => (
          <Skeleton key={index} className="h-32 w-full rounded-xl" />
        ))}
      </div>
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.35fr)_minmax(300px,0.65fr)]">
        <Skeleton className="h-80 w-full rounded-xl" />
        <Skeleton className="h-80 w-full rounded-xl" />
      </div>
      <div className="grid gap-4 xl:grid-cols-2">
        <Skeleton className="h-72 w-full rounded-xl" />
        <Skeleton className="h-72 w-full rounded-xl" />
      </div>
    </div>
  )
}

function formatMoneyLabel(item: ReviewQueueItem) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: item.currency }).format(item.amount)
}
