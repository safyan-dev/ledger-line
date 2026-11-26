import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { GitCompareArrows } from 'lucide-react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { EmptyState } from '@/components/data/empty-state'
import { Money } from '@/components/data/money'
import { StatusBadge } from '@/components/data/status-badge'
import { Skeleton } from '@/components/ui/skeleton'
import { getReconciliationRecords } from '@/features/reconciliation/api'
import { formatRelative } from '@/lib/format'
import { reconciliationOutcomeMeta } from '@/lib/status'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'

export function RecentReconciliation() {
  const tenantId = useTenantKey()
  const records = useQuery({
    queryKey: qk.reconciliation(tenantId, { scope: 'recent' }),
    queryFn: () => getReconciliationRecords({ page: 1, pageSize: 6 }),
  })
  const items = records.data?.items ?? []

  return (
    <Card className="min-w-0 border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader className="flex-row items-start justify-between gap-3 space-y-0 pb-2">
        <div>
          <CardTitle className="text-base">Recent reconciliation</CardTitle>
          <CardDescription>Latest matching decisions and their reasons.</CardDescription>
        </div>
        <Link to="/reconciliation" className="text-xs font-medium text-primary hover:underline">
          Open workspace
        </Link>
      </CardHeader>
      <CardContent className="px-0 pb-0">
        {records.isLoading ? (
          <div className="space-y-3 px-6 py-4">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-10 w-full" />
            ))}
          </div>
        ) : records.isError ? (
          <p className="px-6 py-8 text-center text-xs text-muted-foreground">
            Recent reconciliation activity could not be loaded.
          </p>
        ) : items.length === 0 ? (
          <EmptyState
            icon={<GitCompareArrows className="size-4" aria-hidden />}
            title="No reconciliation activity"
            description="Attempts appear here when payments are matched or need review."
          />
        ) : (
          <ul className="divide-y divide-border/70">
            {items.map((record) => (
              <li key={record.id}>
                <Link
                  to={`/payments/${record.paymentId}`}
                  className="flex min-w-0 items-center justify-between gap-4 px-6 py-3 transition-colors hover:bg-muted/35"
                >
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <StatusBadge tone={reconciliationOutcomeMeta(record.outcome).tone}>
                        {reconciliationOutcomeMeta(record.outcome).label}
                      </StatusBadge>
                      <span className="truncate font-mono text-[11px] text-muted-foreground">
                        {record.providerPaymentId ?? record.paymentId}
                      </span>
                    </div>
                    <p className="mt-1 line-clamp-1 text-[11px] text-muted-foreground">
                      {record.reason ?? record.orderNumber ?? 'No reason recorded'}
                    </p>
                  </div>
                  <div className="shrink-0 text-right">
                    <p className="text-sm font-medium tabular-nums text-foreground">
                      <Money amount={record.actualAmount} currency={record.currency} />
                    </p>
                    <p className="mt-0.5 text-[11px] text-muted-foreground">{formatRelative(record.createdAt)}</p>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
