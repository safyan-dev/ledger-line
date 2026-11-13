import { lazy, Suspense, type ReactNode } from 'react'
import { createBrowserRouter, Link, Navigate, RouterProvider, useRouteError } from 'react-router-dom'
import { AppShell } from '@/components/layout/app-shell'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
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

  if (!session) {
    return <Navigate to="/sign-in" replace />
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
    ],
  },
  { path: '*', element: <NotFoundPage /> },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
