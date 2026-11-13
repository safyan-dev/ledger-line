import { Link } from 'react-router-dom'
import { ArrowRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { SectionHeading } from '@/public-site/components/section-heading'
import { usePageTitle } from '@/lib/use-page-title'

export function PlaceholderPage({
  eyebrow,
  title,
  description,
}: {
  eyebrow: string
  title: string
  description: string
}) {
  usePageTitle(title)

  return (
    <section className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
      <SectionHeading eyebrow={eyebrow} title={title} description={description} />

      <Card className="mt-10 gap-0 border-border/80 py-0">
        <CardContent className="flex flex-col items-start gap-4 p-6 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-semibold text-foreground">This page is on its way.</p>
            <p className="mt-1 text-sm text-muted-foreground">
              We’re putting the finishing touches on this section. In the meantime, explore the product or create
              your workspace.
            </p>
          </div>
          <Button variant="outline" asChild>
            <Link to="/">
              Back to home
              <ArrowRight className="size-4" aria-hidden />
            </Link>
          </Button>
        </CardContent>
      </Card>
    </section>
  )
}
