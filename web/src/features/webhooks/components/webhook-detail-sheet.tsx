import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ExternalLink, Loader2, ShieldCheck } from 'lucide-react'
import { Separator } from '@/components/ui/separator'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { DetailList, DetailRow } from '@/components/data/detail-list'
import { ErrorState } from '@/components/data/error-state'
import { JsonViewer } from '@/components/data/json-viewer'
import { StatusBadge } from '@/components/data/status-badge'
import { getWebhookEvent } from '@/features/webhooks/api'
import { formatDateTime } from '@/lib/format'
import { webhookStatusMeta } from '@/lib/status'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'
import { errorStateProps } from '@/lib/api-client'

export function WebhookDetailSheet({
  eventId,
  onOpenChange,
}: {
  eventId: string | null
  onOpenChange: (open: boolean) => void
}) {
  const tenantId = useTenantKey()
  const query = useQuery({
    queryKey: qk.webhook(tenantId, eventId),
    queryFn: () => getWebhookEvent(eventId!),
    enabled: Boolean(eventId),
  })

  const event = query.data
  const processingMs =
    event?.processedAt && event.receivedAt
      ? Math.max(0, new Date(event.processedAt).getTime() - new Date(event.receivedAt).getTime())
      : null

  return (
    <Sheet open={Boolean(eventId)} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-full overflow-y-auto p-0 sm:max-w-xl">
        <SheetHeader className="border-b px-6 py-4">
          <SheetTitle className="text-base">Webhook event</SheetTitle>
          <SheetDescription className="font-mono text-xs">
            {event ? event.providerEventId : 'Loading event…'}
          </SheetDescription>
        </SheetHeader>

        <div className="space-y-6 px-6 py-5">
          {query.isError ? (
            <ErrorState {...errorStateProps(query.error)} onRetry={() => void query.refetch()} />
          ) : query.isLoading || !event ? (
            <div className="space-y-3">
              <Skeleton className="h-6 w-1/2" />
              <Skeleton className="h-28 w-full" />
              <Skeleton className="h-52 w-full" />
            </div>
          ) : (
            <>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <p className="font-mono text-sm font-medium">{event.eventType}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {formatDateTime(event.receivedAt)} · {event.provider}
                  </p>
                </div>
                <StatusBadge tone={webhookStatusMeta(event.status).tone}>
                  {webhookStatusMeta(event.status).label}
                </StatusBadge>
              </div>

              <DetailList>
                <DetailRow label="Event id">
                  <span className="font-mono text-xs">{event.providerEventId}</span>
                </DetailRow>
                <DetailRow label="Attempts">{event.attemptCount}</DetailRow>
                <DetailRow label="Duplicate deliveries">{event.duplicateCount}</DetailRow>
                <DetailRow label="Conflicts">{event.conflictCount}</DetailRow>
                <DetailRow label="Processed">
                  {event.processedAt ? formatDateTime(event.processedAt) : '—'}
                </DetailRow>
                <DetailRow label="Processing time">
                  {processingMs === null ? '—' : `${processingMs} ms`}
                </DetailRow>
                <DetailRow label="Signature">
                  <span className="inline-flex items-center gap-1.5">
                    <ShieldCheck
                      className={event.hasSignature ? 'size-3.5 text-success' : 'size-3.5 text-muted-foreground'}
                      aria-hidden
                    />
                    {event.hasSignature ? 'Verified (HMAC-SHA256)' : 'Missing'}
                  </span>
                </DetailRow>
                <DetailRow label="Payment">
                  {event.paymentId ? (
                    <Link
                      to="/payments"
                      className="inline-flex items-center gap-1 text-primary hover:underline"
                    >
                      <span className="font-mono text-xs">{event.paymentId.slice(0, 8)}</span>
                      <ExternalLink className="size-3" aria-hidden />
                    </Link>
                  ) : (
                    '—'
                  )}
                </DetailRow>
                {event.failureReason ? <DetailRow label="Failure">{event.failureReason}</DetailRow> : null}
                {event.lastConflictReason ? (
                  <DetailRow label="Last conflict">{event.lastConflictReason}</DetailRow>
                ) : null}
              </DetailList>

              <Separator />

              <div>
                <h3 className="mb-2 text-sm font-semibold">Raw payload</h3>
                <JsonViewer value={event.payload} />
              </div>

              {event.status === 'Processing' ? (
                <p className="flex items-center gap-2 text-xs text-muted-foreground">
                  <Loader2 className="size-3.5 animate-spin motion-reduce:animate-none" aria-hidden />
                  This event is still being processed.
                </p>
              ) : null}
            </>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}
