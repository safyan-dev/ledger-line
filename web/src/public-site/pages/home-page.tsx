import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { ArrowRight, CircleCheck } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { fetchPlans } from '@/public-site/api'
import { FeatureCard } from '@/public-site/components/feature-card'
import { PlanPreviewCard } from '@/public-site/components/plan-preview-card'
import { ReconciliationDiagram } from '@/public-site/components/reconciliation-diagram'
import { ScreenPreview } from '@/public-site/components/screen-preview'
import { SectionHeading } from '@/public-site/components/section-heading'
import { capabilities, capabilityHighlights, problems, steps } from '@/public-site/content'
import { usePageTitle } from '@/lib/use-page-title'

export function HomePage() {
  usePageTitle()

  return (
    <>
      <Hero />
      <Problems />
      <Capabilities />
      <HowItWorks />
      <PricingPreview />
      <FinalCta />
    </>
  )
}

function Hero() {
  return (
    <section className="relative overflow-hidden border-b border-border/70">
      <div className="mx-auto grid w-full max-w-6xl gap-12 px-4 pb-16 pt-14 sm:px-6 sm:pb-20 sm:pt-20 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.05fr)] lg:items-center lg:gap-16 lg:pb-24 lg:pt-24">
        <div className="max-w-xl">
          <p className="inline-flex items-center gap-2 rounded-full border bg-card px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">
            <span className="size-1.5 rounded-full bg-primary" aria-hidden />
            Payment operations
          </p>

          <h1 className="mt-6 text-4xl font-semibold leading-[1.08] tracking-tight text-foreground sm:text-5xl">
            Take control of your payment reconciliation.
          </h1>

          <p className="mt-5 text-base leading-7 text-muted-foreground sm:text-lg">
            Track payments, match transactions with orders, identify discrepancies, and give your finance
            team a clear view of every payment.
          </p>

          <div className="mt-8 flex flex-col gap-3 sm:flex-row">
            <Button size="lg" asChild>
              <Link to="/sign-up">
                Get started free
                <ArrowRight className="size-4" aria-hidden />
              </Link>
            </Button>
            <Button size="lg" variant="outline" asChild>
              <Link to="/demo">Explore live demo</Link>
            </Button>
          </div>

          <ul className="mt-8 flex flex-wrap gap-x-6 gap-y-3">
            {capabilityHighlights.map((highlight) => (
              <li key={highlight.label} className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
                <highlight.icon className="size-4 text-primary" aria-hidden />
                {highlight.label}
              </li>
            ))}
          </ul>
        </div>

        <ScreenPreview />
      </div>
    </section>
  )
}

function Problems() {
  return (
    <section className="border-b border-border/70 bg-card">
      <div className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
        <SectionHeading
          eyebrow="The problem"
          title="Reconciliation breaks where the systems stop talking."
          description="Orders, payments and provider events each tell a different part of the story. Finance is left to assemble it by hand."
        />

        <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {problems.map((problem) => (
            <div key={problem.title} className="rounded-xl border bg-background p-5">
              <h3 className="text-sm font-semibold text-foreground">{problem.title}</h3>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">{problem.description}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}

function Capabilities() {
  return (
    <section className="border-b border-border/70">
      <div className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
        <SectionHeading
          eyebrow="Capabilities"
          title="One workspace for the whole payment lifecycle."
          description="Each capability exists to answer a specific question finance asks when money does not line up."
          action={
            <Button variant="outline" asChild>
              <Link to="/product">
                See all capabilities
                <ArrowRight className="size-4" aria-hidden />
              </Link>
            </Button>
          }
        />

        <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {capabilities.slice(0, 6).map((capability) => (
            <FeatureCard key={capability.key} capability={capability} />
          ))}
        </div>
      </div>
    </section>
  )
}

function HowItWorks() {
  return (
    <section className="border-b border-border/70 bg-card">
      <div className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
        <SectionHeading
          eyebrow="How it works"
          title="From order to a decision finance can defend."
          description="Reconciliation is a pipeline, not a spreadsheet. Every stage leaves a record."
          action={
            <Button variant="outline" asChild>
              <Link to="/how-it-works">
                Walk through the workflow
                <ArrowRight className="size-4" aria-hidden />
              </Link>
            </Button>
          }
        />

        <div className="mt-10 grid gap-8 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] lg:items-center">
          <ol className="space-y-4">
            {steps.slice(0, 4).map((step, index) => (
              <li key={step.title} className="flex gap-4">
                <span className="flex size-7 shrink-0 items-center justify-center rounded-full border bg-background text-xs font-semibold text-primary">
                  {index + 1}
                </span>
                <div>
                  <h3 className="text-sm font-semibold text-foreground">{step.title}</h3>
                  <p className="mt-1 text-sm leading-6 text-muted-foreground">{step.description}</p>
                </div>
              </li>
            ))}
          </ol>

          <div className="rounded-2xl border bg-background p-4 sm:p-6">
            <ReconciliationDiagram className="h-auto w-full" />
            <p className="mt-4 text-center text-xs text-muted-foreground">
              Each comparison produces a recorded outcome and the strategy that produced it.
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}

function PricingPreview() {
  const plans = useQuery({ queryKey: ['plans'], queryFn: fetchPlans, staleTime: 5 * 60_000 })

  return (
    <section className="border-b border-border/70">
      <div className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
        <SectionHeading
          eyebrow="Pricing"
          title="Start free. Move up when volume does."
          description="Every plan includes the reconciliation core. Higher tiers raise usage limits, history and controls."
          action={
            <Button variant="outline" asChild>
              <Link to="/pricing">
                Compare plans
                <ArrowRight className="size-4" aria-hidden />
              </Link>
            </Button>
          }
        />

        <div className="mt-10">
          {plans.isLoading ? (
            <div className="grid gap-4 lg:grid-cols-3" aria-busy="true" aria-label="Loading plans">
              {Array.from({ length: 3 }).map((_, index) => (
                <Card key={index} className="gap-0 border-border/80 py-0">
                  <CardContent className="space-y-4 p-6">
                    <Skeleton className="h-5 w-24" />
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-10 w-32" />
                    <Skeleton className="h-24 w-full" />
                    <Skeleton className="h-9 w-full" />
                  </CardContent>
                </Card>
              ))}
            </div>
          ) : plans.isError || !plans.data ? (
            <Card className="gap-0 border-border/80 py-0">
              <CardContent className="flex flex-col items-start gap-3 p-6 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm font-semibold text-foreground">Plan details are temporarily unavailable.</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    You can still create an account and review plans later.
                  </p>
                </div>
                <Button variant="outline" asChild>
                  <Link to="/pricing">View pricing</Link>
                </Button>
              </CardContent>
            </Card>
          ) : (
            <div className="grid gap-4 lg:grid-cols-3">
              {[...plans.data]
                .sort((left, right) => left.sortOrder - right.sortOrder)
                .map((plan) => (
                  <PlanPreviewCard key={plan.code} plan={plan} />
                ))}
            </div>
          )}
        </div>
      </div>
    </section>
  )
}

function FinalCta() {
  return (
    <section>
      <div className="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
        <div className="rounded-2xl border bg-card px-6 py-10 text-center shadow-[0_1px_2px_rgba(15,23,42,0.04)] sm:px-10">
          <h2 className="text-2xl font-semibold tracking-tight text-foreground sm:text-3xl">
            Know where every payment stands.
          </h2>
          <p className="mx-auto mt-3 max-w-xl text-sm leading-6 text-muted-foreground">
            Create a workspace, record your first orders and payments, and let reconciliation show you what
            needs attention.
          </p>
          <div className="mt-6 flex flex-col justify-center gap-3 sm:flex-row">
            <Button size="lg" asChild>
              <Link to="/sign-up">
                Get started free
                <ArrowRight className="size-4" aria-hidden />
              </Link>
            </Button>
            <Button size="lg" variant="outline" asChild>
              <Link to="/demo">
                <CircleCheck className="size-4" aria-hidden />
                Explore the demo
              </Link>
            </Button>
          </div>
        </div>
      </div>
    </section>
  )
}
