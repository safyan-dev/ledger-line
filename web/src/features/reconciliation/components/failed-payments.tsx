import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CircleX } from 'lucide-react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { EmptyState } from '@/components/data/empty-state'
import { Money } from '@/components/data/money'
import { Skeleton } from '@/components/ui/skeleton'
import { StatusBadge } from '@/components/data/status-badge'
import { getPayments } from '@/features/payments/api'
import { formatDateTime } from '@/lib/format'

export function FailedPayments() {
  const payments = useQuery({
    queryKey: ['payments', 'reconciliation', 'failed'],
    queryFn: () => getPayments({ page: 1, pageSize: 5, status: 'Failed' }),
  })
  const items = payments.data?.items ?? []

  return (
    <Card className="mt-6 border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader className="flex-row items-start justify-between gap-3 space-y-0 pb-2">
        <div>
          <CardTitle className="text-base">Failed payments</CardTitle>
          <CardDescription>
            Provider payments that failed before or during reconciliation
            {payments.data ? ` · ${payments.data.totalCount} total` : ''}.
          </CardDescription>
        </div>
        <Link to="/payments?status=Failed" className="text-xs font-medium text-primary hover:underline">
          View all
        </Link>
      </CardHeader>
      <CardContent className="px-0 pb-0">
        {payments.isLoading ? (
          <div className="space-y-3 px-6 py-4">
            {Array.from({ length: 2 }).map((_, index) => (
              <Skeleton key={index} className="h-10 w-full" />
            ))}
          </div>
        ) : payments.isError ? (
          <p className="px-6 py-8 text-center text-xs text-muted-foreground">Failed payments could not be loaded.</p>
        ) : items.length === 0 ? (
          <EmptyState
            icon={<CircleX className="size-4" aria-hidden />}
            title="No failed payments"
            description="Failed provider transactions will appear here for investigation."
          />
        ) : (
          <ul className="divide-y divide-border/70">
            {items.map((payment) => (
              <li key={payment.id}>
                <Link
                  to={`/payments/${payment.id}`}
                  className="flex flex-wrap items-center justify-between gap-3 px-6 py-3 transition-colors hover:bg-muted/35"
                >
                  <div className="min-w-0">
                    <p className="truncate font-mono text-xs font-medium text-foreground">
                      {payment.providerPaymentId}
                    </p>
                    <p className="mt-0.5 truncate text-[11px] text-muted-foreground">
                      {payment.customerName ?? 'Unknown customer'} · {formatDateTime(payment.receivedAt)}
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-3">
                    <span className="text-sm font-medium tabular-nums text-foreground">
                      <Money amount={payment.amount} currency={payment.currency} />
                    </span>
                    <StatusBadge tone="danger">Failed</StatusBadge>
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
