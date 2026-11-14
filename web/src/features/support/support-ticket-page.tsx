import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, LifeBuoy, Lock, MessageSquareText, Workflow } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { CopyableId } from '@/components/data/copy-button'
import { ErrorState } from '@/components/data/error-state'
import { StatusBadge } from '@/components/data/status-badge'
import { Timeline } from '@/components/data/timeline'
import { TicketReplyForm } from '@/features/support/components/ticket-reply-form'
import { TicketStatusActions } from '@/features/support/components/ticket-status-actions'
import { getTicket } from '@/features/support/api'
import { errorMessage } from '@/lib/api-client'
import { formatDateTime } from '@/lib/format'
import { usePermissions } from '@/lib/permissions'
import {
  supportTicketCategoryMeta,
  supportTicketPriorityMeta,
  supportTicketStatusMeta,
} from '@/lib/status'

export function SupportTicketPage() {
  const { ticketId } = useParams<{ ticketId: string }>()
  const { canResolveTickets } = usePermissions()

  const ticket = useQuery({
    queryKey: ['support', 'ticket', ticketId],
    queryFn: () => getTicket(ticketId!),
    enabled: Boolean(ticketId),
  })

  const back = (
    <Button variant="ghost" size="sm" asChild>
      <Link to="/support/tickets">
        <ArrowLeft className="size-4" aria-hidden />
        Back to tickets
      </Link>
    </Button>
  )

  if (ticket.isError) {
    return (
      <>
        {back}
        <ErrorState message={errorMessage(ticket.error)} onRetry={() => void ticket.refetch()} />
      </>
    )
  }

  if (ticket.isLoading || !ticket.data) {
    return (
      <div className="space-y-4" aria-busy="true" aria-label="Loading support ticket">
        {back}
        <Skeleton className="h-8 w-72" />
        <Skeleton className="h-44 w-full rounded-xl" />
        <Skeleton className="h-52 w-full rounded-xl" />
      </div>
    )
  }

  const { ticket: item, messages } = ticket.data
  const status = supportTicketStatusMeta(item.status)
  const category = supportTicketCategoryMeta(item.category)
  const priority = supportTicketPriorityMeta(item.priority)

  return (
    <>
      {back}
      <PageHeader
        title={item.subject}
        description={`Ticket #${item.id.slice(0, 8)} · Created by ${item.createdBy || 'workspace member'}`}
        actions={<StatusBadge tone={status.tone}>{status.label}</StatusBadge>}
      />

      <div className="space-y-4">
        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader>
            <CardTitle className="text-base">Ticket details</CardTitle>
            <CardDescription>Workspace-scoped support request.</CardDescription>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Detail label="Ticket ID">
                <CopyableId value={item.id} label="ticket id" className="text-xs" />
              </Detail>
              <Detail label="Category">
                <StatusBadge tone={category.tone}>{category.label}</StatusBadge>
              </Detail>
              <Detail label="Priority">
                <StatusBadge tone={priority.tone}>{priority.label}</StatusBadge>
              </Detail>
              <Detail label="Created">
                <span className="text-sm text-foreground">{formatDateTime(item.createdAt)}</span>
              </Detail>
              <Detail label="Last updated">
                <span className="text-sm text-foreground">{formatDateTime(item.updatedAt)}</span>
              </Detail>
              <Detail label="Resolved at">
                <span className="text-sm text-foreground">{item.resolvedAt ? formatDateTime(item.resolvedAt) : '—'}</span>
              </Detail>
              <Detail label="Closed at">
                <span className="text-sm text-foreground">{item.closedAt ? formatDateTime(item.closedAt) : '—'}</span>
              </Detail>
            </dl>
          </CardContent>
        </Card>

        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <MessageSquareText className="size-4 text-primary" aria-hidden />
              Description
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm leading-6 text-foreground">{item.description}</p>
          </CardContent>
        </Card>

        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader>
            <CardTitle className="text-base">Conversation</CardTitle>
            <CardDescription>
              {messages.length === 0 ? 'No replies yet.' : `${messages.length} reply${messages.length === 1 ? '' : 'ies'}.`}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {messages.length === 0 ? (
              <p className="text-sm text-muted-foreground">Replies from the workspace will appear here.</p>
            ) : (
              <Timeline
                items={messages.map((message) => ({
                  id: message.id,
                  title: `${message.authorName} · ${message.authorRole}`,
                  description: message.body,
                  timestamp: formatDateTime(message.createdAt),
                  tone: message.authorRole === 'Owner' ? 'info' : 'neutral',
                }))}
              />
            )}
          </CardContent>
        </Card>

        {item.status === 'Closed' ? (
          <Card className="border-border/80">
            <CardContent className="flex items-center gap-2 py-5 text-sm text-muted-foreground">
              <Lock className="size-4" aria-hidden />
              This ticket is closed. The owner can reopen it if needed.
            </CardContent>
          </Card>
        ) : (
          <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
            <CardHeader>
              <CardTitle className="text-base">Reply to this ticket</CardTitle>
              <CardDescription>Anyone in the workspace can reply.</CardDescription>
            </CardHeader>
            <CardContent>
              <TicketReplyForm ticketId={item.id} />
            </CardContent>
          </Card>
        )}

        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Workflow className="size-4 text-primary" aria-hidden />
              Status workflow
            </CardTitle>
            <CardDescription>
              {canResolveTickets
                ? 'Only valid transitions are offered. The backend validates the final state.'
                : 'Only the workspace owner can change ticket status.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {canResolveTickets ? (
              <TicketStatusActions key={item.status} ticketId={item.id} status={item.status} />
            ) : (
              <p className="flex items-center gap-2 text-xs text-muted-foreground">
                <LifeBuoy className="size-3.5" aria-hidden />
                Ask the workspace owner to move this ticket forward.
              </p>
            )}
          </CardContent>
        </Card>
      </div>
    </>
  )
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-1">{children}</dd>
    </div>
  )
}
