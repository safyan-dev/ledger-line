import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Building2,
  CreditCard,
  LayoutDashboard,
  LifeBuoy,
  LogOut,
  Menu,
  ScrollText,
  type LucideIcon,
} from 'lucide-react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { BrandMark } from '@/components/brand/brand-mark'
import { signOut } from '@/lib/sign-out'
import { useSession } from '@/lib/session'
import { initials } from '@/lib/format'
import { cn } from '@/lib/utils'

const navigation: { label: string; to: string; icon: LucideIcon; end?: boolean }[] = [
  { label: 'Dashboard', to: '/platform', icon: LayoutDashboard, end: true },
  { label: 'Tenants', to: '/platform/tenants', icon: Building2 },
  { label: 'Plans', to: '/platform/plans', icon: ScrollText },
  { label: 'Subscriptions', to: '/platform/subscriptions', icon: CreditCard },
  { label: 'Support', to: '/platform/support', icon: LifeBuoy },
]

function PlatformNavigation({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <nav className="flex flex-col gap-1 p-3" aria-label="Platform administration">
      {navigation.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.end}
          onClick={onNavigate}
          className={({ isActive }) =>
            cn(
              'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60',
              isActive
                ? 'bg-primary/10 text-primary'
                : 'text-muted-foreground hover:bg-muted/70 hover:text-foreground',
            )
          }
        >
          <item.icon className="size-[18px] shrink-0" aria-hidden />
          {item.label}
        </NavLink>
      ))}
    </nav>
  )
}

export function PlatformLayout() {
  const session = useSession()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const logout = useMutation({
    mutationFn: () => signOut(queryClient),
    onSettled: () => navigate('/sign-in', { replace: true }),
  })

  const userName = session?.user.displayName || session?.user.email || 'Platform administrator'
  const userInitials = initials(session?.user.displayName || session?.user.email || 'PA')

  return (
    <div className="min-h-dvh bg-muted/30">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 flex-col border-r border-border/80 bg-card lg:flex">
        <div className="flex h-16 items-center gap-3 border-b border-border/80 px-5">
          <BrandMark />
          <span className="border-l border-border pl-3 text-xs font-semibold uppercase tracking-[0.12em] text-muted-foreground">
            Platform
          </span>
        </div>
        <div className="px-3 pt-5">
          <p className="px-3 text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground/80">
            Administration
          </p>
        </div>
        <div className="flex-1 overflow-y-auto pt-2">
          <PlatformNavigation />
        </div>
        <div className="border-t border-border/80 px-5 py-4">
          <p className="text-xs font-medium text-foreground">Platform operations</p>
          <p className="mt-1 text-[11px] text-muted-foreground">Cross-tenant administration</p>
        </div>
      </aside>

      <div className="flex min-h-dvh min-w-0 flex-col lg:pl-64">
        <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-border/80 bg-card/95 px-4 backdrop-blur lg:px-8">
          <Sheet>
            <SheetTrigger asChild>
              <Button variant="outline" size="icon" className="lg:hidden" aria-label="Open platform navigation">
                <Menu className="size-4" aria-hidden />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-72 p-0">
              <SheetHeader className="border-b border-border/80 px-5 py-4 text-left">
                <SheetTitle className="flex items-center gap-3">
                  <BrandMark />
                  <span className="border-l border-border pl-3 text-xs font-semibold uppercase tracking-[0.12em] text-muted-foreground">
                    Platform
                  </span>
                </SheetTitle>
              </SheetHeader>
              <PlatformNavigation />
            </SheetContent>
          </Sheet>

          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-foreground">Ledgerline Platform</p>
            <p className="hidden text-[11px] text-muted-foreground sm:block">Administration</p>
          </div>

          <div className="ml-auto flex min-w-0 items-center gap-3">
            <Avatar className="size-8">
              <AvatarFallback className="bg-primary/10 text-xs font-semibold text-primary">
                {userInitials}
              </AvatarFallback>
            </Avatar>
            <div className="hidden min-w-0 sm:block">
              <p className="max-w-48 truncate text-xs font-medium text-foreground">{userName}</p>
              <p className="text-[11px] text-muted-foreground">PlatformAdmin</p>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={() => logout.mutate()}
              disabled={logout.isPending}
              aria-label="Log out"
            >
              <LogOut className="size-3.5" aria-hidden />
              <span className="hidden sm:inline">Log out</span>
            </Button>
          </div>
        </header>

        <main className="mx-auto w-full max-w-[1440px] flex-1 px-4 py-6 lg:px-8 lg:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
