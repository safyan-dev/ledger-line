import { NavLink, Outlet } from 'react-router-dom'
import { PageHeader } from '@/components/layout/page-header'
import { usePermissions } from '@/lib/permissions'
import { cn } from '@/lib/utils'
import type { TenantRole } from '@/lib/roles'

const tabs: { to: string; label: string; roles?: TenantRole[] }[] = [
  { to: '/settings/profile', label: 'Profile' },
  { to: '/settings/team', label: 'Team', roles: ['Owner', 'TenantAdmin'] },
  { to: '/settings/subscription', label: 'Subscription & Usage' },
  { to: '/settings/workspace', label: 'Workspace', roles: ['Owner', 'TenantAdmin'] },
]

export function SettingsLayout() {
  const { role } = usePermissions()
  const visibleTabs = tabs.filter((tab) => !tab.roles || (role && tab.roles.includes(role)))

  return (
    <>
      <PageHeader
        title="Settings"
        description="Manage your account, team, subscription and workspace."
      />

      <nav aria-label="Settings sections" className="mb-6 flex flex-wrap gap-1 border-b border-border/70 pb-2">
        {visibleTabs.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            className={({ isActive }) =>
              cn(
                'rounded-md px-3 py-2 text-sm font-medium transition-colors',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60',
                isActive
                  ? 'bg-primary/10 text-primary'
                  : 'text-muted-foreground hover:bg-muted/70 hover:text-foreground',
              )
            }
          >
            {tab.label}
          </NavLink>
        ))}
      </nav>

      <Outlet />
    </>
  )
}
