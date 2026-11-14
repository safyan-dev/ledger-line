import { useSession } from '@/lib/session'
import {
  canManageSubscription,
  canManageTeam,
  canManageWorkspace,
  canResolveTickets,
  normalizeRole,
} from '@/lib/roles'

export function usePermissions() {
  const session = useSession()
  const role = normalizeRole(session?.role)

  return {
    session,
    role,
    tenantName: session?.tenantName ?? null,
    isOwner: role === 'Owner',
    isTenantAdmin: role === 'TenantAdmin',
    isFinance: role === 'Finance',
    canManageTeam: canManageTeam(role),
    canManageWorkspace: canManageWorkspace(role),
    canManageSubscription: canManageSubscription(role),
    canResolveTickets: canResolveTickets(role),
  }
}
