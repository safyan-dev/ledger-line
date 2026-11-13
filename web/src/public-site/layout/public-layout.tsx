import { Outlet } from 'react-router-dom'
import { PublicFooter } from '@/public-site/layout/public-footer'
import { PublicHeader } from '@/public-site/layout/public-header'

export function PublicLayout() {
  return (
    <div className="flex min-h-dvh flex-col bg-background">
      <a
        href="#public-main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-primary focus:px-4 focus:py-2 focus:text-sm focus:font-medium focus:text-primary-foreground"
      >
        Skip to content
      </a>
      <PublicHeader />
      <main id="public-main" className="flex-1">
        <Outlet />
      </main>
      <PublicFooter />
    </div>
  )
}
