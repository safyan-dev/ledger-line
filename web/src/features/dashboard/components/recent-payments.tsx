import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CreditCard } from 'lucide-react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { EmptyState } from '@/components/data/empty-state'
import { Money } from '@/components/data/money'
import { StatusBadge } from '@/components/data/status-badge'
import { Skeleton } from '@/components/ui/skeleton'
import { getPayments } from '@/features/payments/api'
import { formatRelative } from '@/lib/format'
import { paymentStatusMeta } from '@/lib/status'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'

export function RecentPayments() {
  const tenantId = useTenantKey()
  const payments = useQuery({
    queryKey: qk.payments(tenantId, { scope: 'recent' }),
    queryFn: () => getPayments({ page: 1, pageSize: 6 }),
  })
  const items = payments.data?.items ?? []

  return (
    <Card className="min-w-0 border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader className="flex-row items-start justify-between gap-3 space-y-0 pb-2">
        <div>
          <CardTitle className="text-base">Recent payments</CardTitle>
          <CardDescription>Latest provider transactions recorded.</CardDescription>
        </div>
        <Link to="/payments" className="text-xs font-medium text-primary hover:underline">
          View all
        </Link>
      </CardHeader>
      <CardContent className="px-0 pb-0">
        {payments.isLoading ? (
          <div className="space-y-3 px-6 py-4">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-10 w-full" />
            ))}
          </div>
        ) : payments.isError ? (
          <p className="px-6 py-8 text-center text-xs text-muted-foreground">
            Recent payments could not be loaded.
          </p>
        ) : items.length === 0 ? (
          <EmptyState
            icon={<CreditCard className="size-4" aria-hidden />}
            title="No payments yet"
            description="Payments appear here as provider transactions are recorded."
          />
        ) : (
          <ul className="divide-y divide-border/70">
            {items.map((payment) => (
              <li key={payment.id}>
                <Link
                  to={`/payments/${payment.id}`}
                  className="flex min-w-0 items-center justify-between gap-4 px-6 py-3 transition-colors hover:bg-muted/35"
                >
                  <div className="min-w-0">
                    <p className="truncate font-mono text-xs font-medium text-foreground">
                      {payment.providerPaymentId}
                    </p>
                    <p className="mt-0.5 truncate text-[11px] text-muted-foreground">
                      {payment.customerName ?? 'Unknown customer'} · {payment.provider}
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-3">
                    <div className="text-right">
                      <p className="text-sm font-medium tabular-nums text-foreground">
                        <Money amount={payment.amount} currency={payment.currency} />
                      </p>
                      <p className="mt-0.5 text-[11px] text-muted-foreground">{formatRelative(payment.receivedAt)}</p>
                    </div>
                    <StatusBadge tone={paymentStatusMeta(payment.status).tone}>
                      {paymentStatusMeta(payment.status).label}
                    </StatusBadge>
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
