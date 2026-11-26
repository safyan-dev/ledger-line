import { useEffect, useRef } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { AppSidebar } from '@/components/layout/app-sidebar'
import { TopBar } from '@/components/layout/top-bar'
import { useTenantSessionSync } from '@/lib/use-tenant-session-sync'

export function AppShell() {
  useTenantSessionSync()
  const { pathname } = useLocation()
  const mainRef = useRef<HTMLElement>(null)

  // On route change, move focus into the new page so keyboard and screen-reader
  // users are not left on the sidebar link they just activated.
  useEffect(() => {
    const heading = mainRef.current?.querySelector<HTMLElement>('h1')
    if (heading) {
      heading.setAttribute('tabindex', '-1')
      heading.focus({ preventScroll: true })
    }
  }, [pathname])

  return (
    <div className="min-h-dvh bg-background">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-primary focus:px-3 focus:py-2 focus:text-sm focus:font-medium focus:text-primary-foreground focus:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
      >
        Skip to content
      </a>
      <div className="lg:grid lg:grid-cols-[256px_1fr]">
        <AppSidebar />
        <div className="flex min-h-dvh min-w-0 flex-col">
          <TopBar />
          <main
            id="main"
            ref={mainRef}
            className="mx-auto w-full max-w-[1440px] flex-1 px-4 py-6 lg:px-8 lg:py-8"
          >
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  )
}
