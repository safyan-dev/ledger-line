import { NavLink } from 'react-router-dom'
import {
  BarChart3,
  Building2,
  CreditCard,
  GitCompareArrows,
  LayoutDashboard,
  ScrollText,
  ShoppingCart,
  Users,
  Wallet,
  Webhook,
  type LucideIcon,
} from 'lucide-react'
import { BrandMark } from '@/components/brand/brand-mark'
import { usePermissions } from '@/lib/permissions'
import { roleLabel, type TenantRole } from '@/lib/roles'
import { cn } from '@/lib/utils'

type NavItem = {
  to: string
  label: string
  icon: LucideIcon
  end?: boolean
  roles?: TenantRole[]
}

type NavGroup = {
  label: string
  items: NavItem[]
}

const navGroups: NavGroup[] = [
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
      { to: '/reports', label: 'Reports', icon: BarChart3 },
    ],
  },
  {
    label: 'Governance',
    items: [
      { to: '/webhooks', label: 'Webhooks', icon: Webhook },
      { to: '/audit', label: 'Audit history', icon: ScrollText },
    ],
  },
  {
    label: 'Workspace',
    items: [
      { to: '/settings/team', label: 'Team Members', icon: Users, roles: ['Owner', 'TenantAdmin'] },
      { to: '/settings/subscription', label: 'Subscription & Usage', icon: Wallet },
      { to: '/settings/workspace', label: 'Workspace Settings', icon: Building2, roles: ['Owner', 'TenantAdmin'] },
    ],
  },
]

export function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const { role } = usePermissions()

  const visibleGroups = navGroups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => !item.roles || (role && item.roles.includes(role))),
    }))
    .filter((group) => group.items.length > 0)

  return (
    <nav className="flex flex-col gap-5 p-3" aria-label="Primary">
      {visibleGroups.map((group) => (
        <div key={group.label}>
          <p className="px-3 pb-1.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground/80">
            {group.label}
          </p>
          <div className="flex flex-col gap-0.5">
            {group.items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end ?? false}
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
  const { tenantName, role } = usePermissions()

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
          <div className="mt-1.5 flex min-w-0 items-center gap-2">
            <span className="size-1.5 shrink-0 rounded-full bg-success" aria-hidden />
            <p className="truncate text-xs font-medium text-foreground">{tenantName ?? 'Loading workspace…'}</p>
          </div>
          <p className="mt-0.5 text-[11px] text-muted-foreground">{roleLabel(role)}</p>
        </div>
      </div>
    </aside>
  )
}
