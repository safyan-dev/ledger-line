import { cn } from '@/lib/utils'
import { formatMoney } from '@/lib/format'

export function Money({
  amount,
  currency = 'USD',
  className,
}: {
  amount: number
  currency?: string
  className?: string
}) {
  return <span className={cn('tabular-nums', className)}>{formatMoney(amount, currency)}</span>
}
