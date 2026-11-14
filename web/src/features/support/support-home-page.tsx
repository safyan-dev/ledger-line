import { Link } from 'react-router-dom'
import { ArrowRight, LifeBuoy, MessageSquareText, Ticket } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { CreateTicketDialog } from '@/features/support/components/create-ticket-dialog'

export function SupportHomePage() {
  return (
    <>
      <PageHeader
        title="Help & support"
        description="Create a ticket for your workspace or review existing conversations."
        actions={<CreateTicketDialog />}
      />

      <div className="grid gap-4 md:grid-cols-2">
        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Ticket className="size-4 text-primary" aria-hidden />
              Support tickets
            </CardTitle>
            <CardDescription>View the workspace ticket history and continue open conversations.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button asChild variant="outline">
              <Link to="/support/tickets">
                Browse tickets
                <ArrowRight className="size-4" aria-hidden />
              </Link>
            </Button>
          </CardContent>
        </Card>

        <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <MessageSquareText className="size-4 text-primary" aria-hidden />
              How support works
            </CardTitle>
            <CardDescription>
              Every ticket is scoped to this workspace. All roles can create, view and reply; only the
              owner resolves or closes tickets.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex items-start gap-2 rounded-lg border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
              <LifeBuoy className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              Categories: technical, payment, reconciliation, account, billing or other. Priorities: low,
              normal or high.
            </div>
          </CardContent>
        </Card>
      </div>
    </>
  )
}
