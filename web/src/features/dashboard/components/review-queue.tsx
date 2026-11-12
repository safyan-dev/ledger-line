import { ArrowRight, ArrowUpRight, Inbox, Undo2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { EmptyState } from '@/components/data/empty-state'
import { formatAge, formatMoney } from '@/lib/format'
import type { ReviewQueueItem } from '@/types/api'

export function ReviewQueue({
  items,
  onMatch,
  onRefund,
}: {
  items: ReviewQueueItem[]
  onMatch: (item: ReviewQueueItem) => void
  onRefund: (item: ReviewQueueItem) => void
}) {
  return (
    <Card className="flex h-full min-w-0 flex-col border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Review queue</CardTitle>
        <CardDescription>Unmatched, amount-mismatch and currency-mismatch payments. Oldest first.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-1 flex-col px-0 pb-2">
        {items.length === 0 ? (
          <EmptyState
            icon={<Inbox className="size-4" aria-hidden />}
            title="Queue is clear"
            description="Every recorded payment has been matched or refunded."
          />
        ) : (
          <ul className="divide-y divide-border/70">
            {items.map((item) => (
              <li key={item.paymentId} className="flex flex-wrap items-center justify-between gap-2 px-6 py-3">
                <div className="min-w-0 flex-1">
                  <Link
                    to={`/payments/${item.paymentId}`}
                    className="block truncate font-mono text-xs font-medium text-foreground hover:text-primary hover:underline"
                  >
                    {item.providerPaymentId}
                  </Link>
                  <p className="mt-0.5 truncate text-[11px] text-muted-foreground">
                    {item.customerName ?? 'Unknown customer'} · waiting {formatAge(item.ageHours)}
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-1.5">
                  <span className="mr-1 text-sm font-medium tabular-nums">{formatMoney(item.amount, item.currency)}</span>
                  <Button variant="outline" size="sm" className="h-7 px-2 text-xs" onClick={() => onMatch(item)}>
                    Match
                    <ArrowUpRight className="size-3" aria-hidden />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 px-2 text-xs text-muted-foreground"
                    onClick={() => onRefund(item)}
                  >
                    <Undo2 className="size-3" aria-hidden />
                    Refund
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
        <div className="mt-auto flex items-center justify-between gap-3 border-t border-border/70 px-6 pt-3 text-[11px] text-muted-foreground">
          <span>Oldest exceptions shown first</span>
          <Link to="/payments?status=Unmatched" className="inline-flex items-center gap-1 font-medium text-primary hover:underline">
            Open payments
            <ArrowRight className="size-3" aria-hidden />
          </Link>
        </div>
      </CardContent>
    </Card>
  )
}
