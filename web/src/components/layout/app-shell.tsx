import { Outlet } from 'react-router-dom'
import { AppSidebar } from '@/components/layout/app-sidebar'
import { TopBar } from '@/components/layout/top-bar'

export function AppShell() {
  return (
    <div className="min-h-dvh bg-background">
      <div className="lg:grid lg:grid-cols-[256px_1fr]">
        <AppSidebar />
        <div className="flex min-h-dvh min-w-0 flex-col">
          <TopBar />
          <main className="mx-auto w-full max-w-[1440px] flex-1 px-4 py-6 lg:px-8 lg:py-8">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  )
}
