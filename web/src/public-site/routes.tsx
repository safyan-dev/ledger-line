import type { RouteObject } from 'react-router-dom'
import { AuthLayout } from '@/public-site/layout/auth-layout'
import { PublicLayout } from '@/public-site/layout/public-layout'
import { HomePage } from '@/public-site/pages/home-page'
import { PlaceholderPage } from '@/public-site/pages/placeholder-page'
import { SignInPage } from '@/public-site/pages/sign-in-page'
import { SignUpPage } from '@/public-site/pages/sign-up-page'

export const publicSiteRoutes: RouteObject[] = [
  {
    path: '/',
    element: <PublicLayout />,
    children: [
      { index: true, element: <HomePage /> },
      {
        path: 'product',
        element: (
          <PlaceholderPage
            eyebrow="Product"
            title="Every payment, order and decision in one place."
            description="Payment tracking, order management, webhook processing, duplicate handling, reconciliation and audit history."
          />
        ),
      },
      {
        path: 'how-it-works',
        element: (
          <PlaceholderPage
            eyebrow="How it works"
            title="From order to a reconciliation decision."
            description="The business workflow, from order capture through matching, classification and review."
          />
        ),
      },
      {
        path: 'pricing',
        element: (
          <PlaceholderPage
            eyebrow="Pricing"
            title="Plans that scale with payment volume."
            description="Compare plans, features and usage limits, and choose the plan that fits your payment volume."
          />
        ),
      },
      {
        path: 'about',
        element: (
          <PlaceholderPage
            eyebrow="About"
            title="Built for the team that has to explain the numbers."
            description="Why Ledgerline exists, the reconciliation problem it addresses and who it is for."
          />
        ),
      },
      {
        path: 'demo',
        element: (
          <PlaceholderPage
            eyebrow="Demo"
            title="Explore reconciliation with demonstration data."
            description="A read-only walkthrough of reconciliation using demonstration data."
          />
        ),
      },
    ],
  },
  {
    element: <AuthLayout />,
    children: [
      { path: '/sign-in', element: <SignInPage /> },
      { path: '/sign-up', element: <SignUpPage /> },
    ],
  },
]
