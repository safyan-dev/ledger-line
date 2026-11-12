import { History } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { EmptyState } from '@/components/data/empty-state'
import { StatusBadge } from '@/components/data/status-badge'
import { formatRelative } from '@/lib/format'
import { moduleLabel } from '@/lib/status'
import type { ActivityItem } from '@/types/api'

export function RecentActivity({ items }: { items: ActivityItem[] }) {
  return (
    <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Recent activity</CardTitle>
        <CardDescription>Business actions captured in the audit log.</CardDescription>
      </CardHeader>
      <CardContent className="px-0 pb-0">
        {items.length === 0 ? (
          <EmptyState
            icon={<History className="size-4" aria-hidden />}
            title="No activity yet"
            description="Business actions appear here once orders, payments or reconciliation attempts are recorded."
          />
        ) : (
          <ul className="divide-y divide-border/70">
            {items.map((item) => (
              <li key={item.id} className="flex items-start justify-between gap-4 px-6 py-3">
                <div className="flex min-w-0 items-start gap-3">
                  <StatusBadge tone={item.action.includes('unmatched') || item.action.includes('failed') ? 'warning' : 'neutral'}>
                    {moduleLabel(item.module)}
                  </StatusBadge>
                  <div className="min-w-0">
                    <p className="truncate text-sm text-foreground">{item.summary}</p>
                    <p className="mt-0.5 text-[11px] text-muted-foreground">
                      {item.actorName} · {formatRelative(item.occurredAt)}
                    </p>
                  </div>
                </div>
                <Link
                  to={`/audit?entityType=${encodeURIComponent(item.entityType)}${item.entityId ? `&entityId=${item.entityId}` : ''}`}
                  className="shrink-0 text-[11px] font-medium text-primary hover:underline"
                >
                  Trace
                </Link>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
