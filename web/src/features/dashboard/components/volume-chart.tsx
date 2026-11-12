import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { EmptyState } from '@/components/data/empty-state'
import { formatMoney, formatShortDate } from '@/lib/format'
import type { VolumePoint } from '@/types/api'

export function VolumeChart({ volume }: { volume: VolumePoint[] }) {
  const hasVolume = volume.some(
    (point) => point.matchedAmount !== 0 || point.unmatchedAmount !== 0 || point.manualAmount !== 0,
  )

  return (
    <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader className="flex-row items-start justify-between space-y-0 pb-2">
        <div>
          <CardTitle className="text-base">Payment volume</CardTitle>
          <CardDescription>Matched, unmatched and manual amounts over the last 14 days.</CardDescription>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-3 text-[11px] text-muted-foreground">
          <span className="flex items-center gap-1.5">
            <span className="size-2 rounded-full bg-success" aria-hidden /> Matched
          </span>
          <span className="flex items-center gap-1.5">
            <span className="size-2 rounded-full bg-warning" aria-hidden /> Unmatched
          </span>
          <span className="flex items-center gap-1.5">
            <span className="size-2 rounded-full bg-primary" aria-hidden /> Manual
          </span>
        </div>
      </CardHeader>
      <CardContent className="pt-2">
        {hasVolume ? (
          <div className="h-[240px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={volume} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
              <defs>
                <linearGradient id="matched-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--success)" stopOpacity={0.25} />
                  <stop offset="100%" stopColor="var(--success)" stopOpacity={0.02} />
                </linearGradient>
                <linearGradient id="unmatched-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--warning)" stopOpacity={0.25} />
                  <stop offset="100%" stopColor="var(--warning)" stopOpacity={0.02} />
                </linearGradient>
                <linearGradient id="manual-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--primary)" stopOpacity={0.2} />
                  <stop offset="100%" stopColor="var(--primary)" stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
              <XAxis
                dataKey="date"
                tickFormatter={formatShortDate}
                tickLine={false}
                axisLine={false}
                tick={{ fontSize: 11, fill: 'var(--muted-foreground)' }}
                minTickGap={18}
              />
              <YAxis
                tickFormatter={(value: number) => `$${Math.round(value / 100) / 10}k`}
                tickLine={false}
                axisLine={false}
                width={44}
                tick={{ fontSize: 11, fill: 'var(--muted-foreground)' }}
              />
              <Tooltip
                formatter={(value) => formatMoney(Number(value))}
                labelFormatter={(label) => formatShortDate(String(label))}
                contentStyle={{
                  borderRadius: 10,
                  border: '1px solid var(--border)',
                  background: 'var(--popover)',
                  fontSize: 12,
                  boxShadow: '0 8px 24px rgba(15,23,42,0.08)',
                }}
              />
              <Area
                type="monotone"
                dataKey="matchedAmount"
                name="Matched"
                stackId="amount"
                stroke="var(--success)"
                strokeWidth={1.8}
                fill="url(#matched-fill)"
                isAnimationActive={false}
              />
              <Area
                type="monotone"
                dataKey="unmatchedAmount"
                name="Unmatched"
                stackId="amount"
                stroke="var(--warning)"
                strokeWidth={1.8}
                fill="url(#unmatched-fill)"
                isAnimationActive={false}
              />
              <Area
                type="monotone"
                dataKey="manualAmount"
                name="Manual"
                stackId="amount"
                stroke="var(--primary)"
                strokeWidth={1.8}
                fill="url(#manual-fill)"
                isAnimationActive={false}
              />
            </AreaChart>
          </ResponsiveContainer>
          </div>
        ) : (
          <EmptyState
            title="No payment volume yet"
            description="Volume appears once payments are recorded in the last 14 days."
          />
        )}
      </CardContent>
    </Card>
  )
}
