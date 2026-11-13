import { WalletCards } from 'lucide-react'
import { cn } from '@/lib/utils'

export function BrandMark({ className, subtitle = true }: { className?: string; subtitle?: boolean }) {
  return (
    <div className={cn('flex items-center gap-2.5', className)}>
      <div className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground shadow-sm">
        <WalletCards className="size-4" aria-hidden />
      </div>
      <div className="leading-none">
        <p className="text-[15px] font-semibold tracking-tight text-foreground">Ledgerline</p>
        {subtitle ? (
          <p className="mt-1 text-[11px] font-medium text-muted-foreground">Payment reconciliation</p>
        ) : null}
      </div>
    </div>
  )
}
