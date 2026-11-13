import { Card, CardContent } from '@/components/ui/card'
import { StatusBadge } from '@/components/data/status-badge'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

const metrics = [
  { label: 'Matched', value: '1,284', tone: 'text-success' },
  { label: 'Unmatched', value: '18', tone: 'text-warning-foreground' },
  { label: 'Mismatches', value: '4', tone: 'text-destructive' },
]

const rows = [
  { reference: 'ORD-1042', amount: '$248.00', status: 'Matched', tone: 'success' as const },
  { reference: 'ORD-1043', amount: '$96.50', status: 'Unmatched', tone: 'warning' as const },
  { reference: 'ORD-1044', amount: '$1,200.00', status: 'Amount mismatch', tone: 'danger' as const },
  { reference: 'ORD-1045', amount: '$72.00', status: 'Matched', tone: 'success' as const },
]

export function ScreenPreview() {
  return (
    <div
      className="relative"
      role="img"
      aria-label="Preview of the Ledgerline reconciliation dashboard: matched, unmatched and mismatched payment totals with a payment table."
    >
      <div className="absolute -inset-6 -z-10 rounded-[2rem] bg-gradient-to-br from-primary/10 via-transparent to-transparent blur-2xl" aria-hidden />

      <Card className="overflow-hidden border-border/80 py-0 shadow-[0_18px_50px_-24px_rgba(15,23,42,0.35)]">
        <div className="flex items-center justify-between border-b bg-muted/40 px-4 py-2.5">
          <div className="flex items-center gap-1.5" aria-hidden>
            <span className="size-2 rounded-full bg-destructive/50" />
            <span className="size-2 rounded-full bg-warning/60" />
            <span className="size-2 rounded-full bg-success/50" />
          </div>
          <span className="text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">
            Product preview
          </span>
        </div>

        <CardContent className="space-y-4 p-4" aria-hidden>
          <div className="grid grid-cols-3 gap-2">
            {metrics.map((metric) => (
              <div key={metric.label} className="rounded-lg border bg-background px-3 py-2.5">
                <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{metric.label}</p>
                <p className={`mt-1 text-lg font-semibold tabular-nums tracking-tight ${metric.tone}`}>{metric.value}</p>
              </div>
            ))}
          </div>

          <div className="rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="h-8 px-3 text-[11px]">Reference</TableHead>
                  <TableHead className="h-8 px-3 text-right text-[11px]">Amount</TableHead>
                  <TableHead className="h-8 px-3 text-right text-[11px]">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.reference} className="hover:bg-transparent">
                    <TableCell className="px-3 py-2 font-medium">{row.reference}</TableCell>
                    <TableCell className="px-3 py-2 text-right tabular-nums text-muted-foreground">{row.amount}</TableCell>
                    <TableCell className="px-3 py-2 text-right">
                      <StatusBadge tone={row.tone}>{row.status}</StatusBadge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
