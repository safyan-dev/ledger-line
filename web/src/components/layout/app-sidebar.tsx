import { NavLink } from 'react-router-dom'
import {
  CreditCard,
  GitCompareArrows,
  LayoutDashboard,
  ScrollText,
  ShoppingCart,
  Webhook,
} from 'lucide-react'
import { BrandMark } from '@/components/brand/brand-mark'
import { cn } from '@/lib/utils'

const navGroups = [
  {
    label: 'Overview',
    items: [{ to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard, end: true }],
  },
  {
    label: 'Operations',
    items: [
      { to: '/orders', label: 'Orders', icon: ShoppingCart },
      { to: '/payments', label: 'Payments', icon: CreditCard },
      { to: '/reconciliation', label: 'Reconciliation', icon: GitCompareArrows },
      { to: '/webhooks', label: 'Webhooks', icon: Webhook },
    ],
  },
  {
    label: 'Governance',
    items: [{ to: '/audit', label: 'Audit history', icon: ScrollText }],
  },
] as const

export function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <nav className="flex flex-col gap-5 p-3" aria-label="Primary">
      {navGroups.map((group) => (
        <div key={group.label}>
          <p className="px-3 pb-1.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground/80">
            {group.label}
          </p>
          <div className="flex flex-col gap-0.5">
            {group.items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={'end' in item ? item.end : false}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'group flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60',
                    isActive
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:bg-muted/70 hover:text-foreground',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon
                      className={cn('size-[18px] shrink-0', isActive ? 'text-primary' : 'text-muted-foreground')}
                      aria-hidden
                    />
                    {item.label}
                  </>
                )}
              </NavLink>
            ))}
          </div>
        </div>
      ))}
    </nav>
  )
}

export function AppSidebar() {
  return (
    <aside className="sticky top-0 hidden h-dvh w-64 shrink-0 flex-col border-r border-sidebar-border bg-sidebar lg:flex">
      <div className="flex h-16 items-center border-b border-sidebar-border px-5">
        <BrandMark />
      </div>
      <div className="flex-1 overflow-y-auto py-3">
        <SidebarNav />
      </div>
      <div className="border-t border-sidebar-border p-3">
        <div className="rounded-lg border border-sidebar-border bg-background px-3 py-2.5">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Workspace</p>
          <div className="mt-1.5 flex items-center gap-2">
            <span className="size-1.5 rounded-full bg-success" aria-hidden />
            <p className="text-xs font-medium text-foreground">Active</p>
          </div>
        </div>
      </div>
    </aside>
  )
}
