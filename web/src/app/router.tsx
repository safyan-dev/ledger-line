import { lazy, Suspense, useEffect, useRef, type ReactNode } from 'react'
import { createBrowserRouter, Link, Navigate, RouterProvider, useRouteError } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { AppShell } from '@/components/layout/app-shell'
import { RequirePlatformAdmin } from '@/components/layout/require-platform-admin'
import { PlatformLayout } from '@/components/layout/platform-layout'
import { RequireRole } from '@/components/layout/require-role'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { SettingsLayout } from '@/features/settings/settings-layout'
import { publicSiteRoutes } from '@/public-site/routes'
import { useSession } from '@/lib/session'

const DashboardPage = lazy(() =>
  import('@/features/dashboard/dashboard-page').then((module) => ({ default: module.DashboardPage })),
)
const OrdersPage = lazy(() =>
  import('@/features/orders/orders-page').then((module) => ({ default: module.OrdersPage })),
)
const OrderDetailPage = lazy(() =>
  import('@/features/orders/order-detail-page').then((module) => ({ default: module.OrderDetailPage })),
)
const PaymentsPage = lazy(() =>
  import('@/features/payments/payments-page').then((module) => ({ default: module.PaymentsPage })),
)
const PaymentDetailPage = lazy(() =>
  import('@/features/payments/payment-detail-page').then((module) => ({ default: module.PaymentDetailPage })),
)
const WebhooksPage = lazy(() =>
  import('@/features/webhooks/webhooks-page').then((module) => ({ default: module.WebhooksPage })),
)
const ReconciliationPage = lazy(() =>
  import('@/features/reconciliation/reconciliation-page').then((module) => ({ default: module.ReconciliationPage })),
)
const AuditPage = lazy(() =>
  import('@/features/audit/audit-page').then((module) => ({ default: module.AuditPage })),
)
const AccountPage = lazy(() =>
  import('@/features/account/account-page').then((module) => ({ default: module.AccountPage })),
)
const TeamPage = lazy(() =>
  import('@/features/team/team-page').then((module) => ({ default: module.TeamPage })),
)
const SubscriptionPage = lazy(() =>
  import('@/features/subscription/subscription-page').then((module) => ({ default: module.SubscriptionPage })),
)
const WorkspaceSettingsPage = lazy(() =>
  import('@/features/workspace/workspace-settings-page').then((module) => ({
    default: module.WorkspaceSettingsPage,
  })),
)
const ReportsPage = lazy(() =>
  import('@/features/reports/reports-page').then((module) => ({ default: module.ReportsPage })),
)
const SupportHomePage = lazy(() =>
  import('@/features/support/support-home-page').then((module) => ({ default: module.SupportHomePage })),
)
const SupportTicketsPage = lazy(() =>
  import('@/features/support/support-tickets-page').then((module) => ({
    default: module.SupportTicketsPage,
  })),
)
const SupportTicketPage = lazy(() =>
  import('@/features/support/support-ticket-page').then((module) => ({
    default: module.SupportTicketPage,
  })),
)
const PlatformTenantsPage = lazy(() =>
  import('@/features/platform/platform-page').then((module) => ({ default: module.PlatformPage })),
)
const PlatformDashboardPage = lazy(() =>
  import('@/features/platform/platform-dashboard-page').then((module) => ({ default: module.PlatformDashboardPage })),
)
const PlatformTenantDetailPage = lazy(() =>
  import('@/features/platform/platform-tenant-detail-page').then((module) => ({
    default: module.PlatformTenantDetailPage,
  })),
)
const PlatformPlansPage = lazy(() =>
  import('@/features/platform/platform-plans-page').then((module) => ({
    default: module.PlatformPlansPage,
  })),
)
const PlatformSubscriptionsPage = lazy(() =>
  import('@/features/platform/platform-subscriptions-page').then((module) => ({
    default: module.PlatformSubscriptionsPage,
  })),
)
const PlatformSupportPage = lazy(() =>
  import('@/features/platform/platform-support-page').then((module) => ({
    default: module.PlatformSupportPage,
  })),
)

function PageSkeleton() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-4 w-96" />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <Skeleton key={index} className="h-32 w-full rounded-xl" />
        ))}
      </div>
      <Skeleton className="h-80 w-full rounded-xl" />
    </div>
  )
}

function withSuspense(element: ReactNode) {
  return <Suspense fallback={<PageSkeleton />}>{element}</Suspense>
}

function RequireSession({ children }: { children: ReactNode }) {
  const session = useSession()
  const queryClient = useQueryClient()
  const tenantId = session?.tenantId ?? null
  const previousTenantId = useRef<string | null>(tenantId)

  // Every cached query is tenant-scoped data. Signing out or switching workspace
  // must drop the cache, otherwise the previous tenant's rows stay fresh for the
  // whole staleTime and render under the new tenant.
  useEffect(() => {
    if (!session) {
      queryClient.clear()
      previousTenantId.current = null
      return
    }

    if (previousTenantId.current !== tenantId) {
      queryClient.clear()
      previousTenantId.current = tenantId
    }
  }, [session, tenantId, queryClient])

  if (!session) {
    return <Navigate to="/sign-in" replace />
  }

  if (session.user.isPlatformAdmin && !session.tenantSlug) {
    return <Navigate to="/platform" replace />
  }

  return <>{children}</>
}

function RouteError() {
  const error = useRouteError()
  const message = error instanceof Error ? error.message : 'The page could not be rendered.'

  return (
    <div className="mx-auto flex min-h-dvh max-w-lg flex-col items-center justify-center gap-4 px-6 text-center">
      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Something broke</p>
      <h1 className="text-2xl font-semibold tracking-tight">This screen failed to load</h1>
      <p className="text-sm text-muted-foreground">{message}</p>
      <Button asChild>
        <Link to="/">Back to home</Link>
      </Button>
    </div>
  )
}

function NotFoundPage() {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-3 px-6 text-center">
      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">404</p>
      <h1 className="text-2xl font-semibold tracking-tight">Page not found</h1>
      <p className="max-w-sm text-sm text-muted-foreground">
        The link may be stale. Use the navigation to find the page you need.
      </p>
      <Button asChild>
        <Link to="/">Back to home</Link>
      </Button>
    </div>
  )
}

const router = createBrowserRouter([
  ...publicSiteRoutes,
  {
    path: '/platform',
    element: (
      <RequirePlatformAdmin>
        <PlatformLayout />
      </RequirePlatformAdmin>
    ),
    errorElement: <RouteError />,
    children: [
      {
        index: true,
        element: withSuspense(<PlatformDashboardPage />),
      },
      { path: 'tenants', element: withSuspense(<PlatformTenantsPage />) },
      { path: 'tenants/:tenantId', element: withSuspense(<PlatformTenantDetailPage />) },
      { path: 'plans', element: withSuspense(<PlatformPlansPage />) },
      { path: 'subscriptions', element: withSuspense(<PlatformSubscriptionsPage />) },
      { path: 'support', element: withSuspense(<PlatformSupportPage />) },
    ],
  },
  {
    element: (
      <RequireSession>
        <AppShell />
      </RequireSession>
    ),
    errorElement: <RouteError />,
    children: [
      { path: '/dashboard', element: withSuspense(<DashboardPage />) },
      { path: '/orders', element: withSuspense(<OrdersPage />) },
      { path: '/orders/:orderId', element: withSuspense(<OrderDetailPage />) },
      { path: '/payments', element: withSuspense(<PaymentsPage />) },
      { path: '/payments/:paymentId', element: withSuspense(<PaymentDetailPage />) },
      { path: '/webhooks', element: withSuspense(<WebhooksPage />) },
      { path: '/reconciliation', element: withSuspense(<ReconciliationPage />) },
      { path: '/audit', element: withSuspense(<AuditPage />) },
      { path: '/reports', element: withSuspense(<ReportsPage />) },
      {
        path: '/settings',
        element: withSuspense(<SettingsLayout />),
        children: [
          { index: true, element: <Navigate to="/settings/profile" replace /> },
          { path: 'profile', element: withSuspense(<AccountPage />) },
          {
            path: 'team',
            element: withSuspense(
              <RequireRole
                allow={['Owner', 'TenantAdmin']}
                title="Team management is restricted"
                description="Only workspace owners and tenant admins can manage team members."
              >
                <TeamPage />
              </RequireRole>,
            ),
          },
          { path: 'subscription', element: withSuspense(<SubscriptionPage />) },
          {
            path: 'workspace',
            element: withSuspense(
              <RequireRole
                allow={['Owner', 'TenantAdmin']}
                title="Workspace settings are restricted"
                description="Only workspace owners and tenant admins can change workspace settings."
              >
                <WorkspaceSettingsPage />
              </RequireRole>,
            ),
          },
        ],
      },
      { path: '/support', element: withSuspense(<SupportHomePage />) },
      { path: '/support/tickets', element: withSuspense(<SupportTicketsPage />) },
      { path: '/support/tickets/:ticketId', element: withSuspense(<SupportTicketPage />) },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
