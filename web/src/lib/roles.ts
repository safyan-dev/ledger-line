export type TenantRole = 'Owner' | 'TenantAdmin' | 'Finance'

export function normalizeRole(value: string | null | undefined): TenantRole | null {
  if (!value) return null
  if (value === 'Owner') return 'Owner'
  if (value === 'TenantAdmin' || value === 'Admin') return 'TenantAdmin'
  if (value === 'Finance') return 'Finance'
  return null
}

export function roleLabel(role: TenantRole | null): string {
  if (role === 'TenantAdmin') return 'Tenant admin'
  if (role === 'Owner') return 'Owner'
  if (role === 'Finance') return 'Finance'
  return 'Member'
}

export function canManageTeam(role: TenantRole | null): boolean {
  return role === 'Owner' || role === 'TenantAdmin'
}

export function canManageWorkspace(role: TenantRole | null): boolean {
  return role === 'Owner' || role === 'TenantAdmin'
}

export function canManageSubscription(role: TenantRole | null): boolean {
  return role === 'Owner'
}

export function canResolveTickets(role: TenantRole | null): boolean {
  return role === 'Owner'
}

export const teamRoles: TenantRole[] = ['Owner', 'TenantAdmin']
