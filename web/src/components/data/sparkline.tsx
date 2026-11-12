import { useId } from 'react'
import { Area, AreaChart, ResponsiveContainer } from 'recharts'
import { cn } from '@/lib/utils'

export function Sparkline({
  data,
  tone = 'primary',
  className,
}: {
  data: number[]
  tone?: 'primary' | 'success' | 'warning'
  className?: string
}) {
  const gradientId = useId()
  const stroke = tone === 'success' ? 'var(--success)' : tone === 'warning' ? 'var(--warning)' : 'var(--primary)'
  const chartData = data.map((value, index) => ({ index, value }))

  return (
    <div className={cn('h-11 w-24 shrink-0', className)} aria-hidden>
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={chartData} margin={{ top: 3, right: 0, bottom: 0, left: 0 }}>
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={stroke} stopOpacity={0.28} />
              <stop offset="100%" stopColor={stroke} stopOpacity={0.02} />
            </linearGradient>
          </defs>
          <Area
            type="monotone"
            dataKey="value"
            stroke={stroke}
            strokeWidth={1.6}
            fill={`url(#${gradientId})`}
            isAnimationActive={false}
            dot={false}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  )
}
