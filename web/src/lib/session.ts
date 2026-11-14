import type { AuthResponse, AuthUser, Membership } from '@/lib/auth-api'
import { useSyncExternalStore } from 'react'
import { normalizeRole, type TenantRole } from '@/lib/roles'

export type AuthSession = {
  accessToken: string
  refreshToken: string
  tenantId: string | null
  tenantName: string | null
  tenantSlug: string | null
  role: TenantRole | null
  user: AuthUser
}

export type SelectedPlan = {
  code: string
  name: string
}

const SESSION_KEY = 'ledgerline.session'
const PLAN_KEY = 'ledgerline.selectedPlan'
const TENANT_KEY_PREFIX = 'ledgerline.tenant.'

const listeners = new Set<() => void>()
let cachedSession: AuthSession | null | undefined

function readSession(): AuthSession | null {
  try {
    const raw = window.localStorage.getItem(SESSION_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<AuthSession>
    if (!parsed.accessToken || !parsed.refreshToken || !parsed.user) return null
    return {
      accessToken: parsed.accessToken,
      refreshToken: parsed.refreshToken,
      tenantId: parsed.tenantId ?? null,
      tenantName: parsed.tenantName ?? null,
      tenantSlug: parsed.tenantSlug ?? null,
      role: normalizeRole(parsed.role),
      user: parsed.user,
    }
  } catch {
    return null
  }
}

function emit() {
  cachedSession = readSession()
  for (const listener of listeners) listener()
}

export function loadSession(): AuthSession | null {
  if (cachedSession === undefined) cachedSession = readSession()
  return cachedSession
}

export function saveSession(session: AuthSession): void {
  window.localStorage.setItem(SESSION_KEY, JSON.stringify(session))
  emit()
}

export function clearSession(): void {
  window.localStorage.removeItem(SESSION_KEY)
  emit()
}

export function clearTenantStorage(): void {
  try {
    const keys: string[] = []
    for (let index = 0; index < window.localStorage.length; index += 1) {
      const key = window.localStorage.key(index)
      if (key?.startsWith(TENANT_KEY_PREFIX)) keys.push(key)
    }
    for (const key of keys) window.localStorage.removeItem(key)
  } finally {
    clearSession()
  }
}

export function updateSessionUser(user: AuthUser): void {
  const current = loadSession()
  if (!current) return
  saveSession({ ...current, user })
}

export function updateSessionTenant(tenant: {
  tenantId: string | null
  tenantName: string | null
  tenantSlug: string | null
  role: TenantRole | null
}): void {
  const current = loadSession()
  if (!current) return

  if (
    current.tenantId === tenant.tenantId &&
    current.tenantName === tenant.tenantName &&
    current.tenantSlug === tenant.tenantSlug &&
    current.role === tenant.role
  ) {
    return
  }

  saveSession({ ...current, ...tenant })
}

export function subscribeSession(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function getSelectedPlan(): SelectedPlan | null {
  try {
    const raw = window.localStorage.getItem(PLAN_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<SelectedPlan>
    return parsed.code && parsed.name ? { code: parsed.code, name: parsed.name } : null
  } catch {
    return null
  }
}

export function setSelectedPlan(plan: SelectedPlan | null): void {
  if (!plan) {
    window.localStorage.removeItem(PLAN_KEY)
    return
  }

  window.localStorage.setItem(PLAN_KEY, JSON.stringify(plan))
}

export function signUpHref(): string {
  const plan = getSelectedPlan()
  return plan ? `/sign-up?plan=${encodeURIComponent(plan.code)}` : '/sign-up'
}

export function useSession(): AuthSession | null {
  return useSyncExternalStore(subscribeSession, loadSession, () => null)
}

export type SessionTenant = Pick<Membership, 'tenantId' | 'name' | 'slug' | 'role'>

export function sessionFromAuth(
  response: AuthResponse,
  tenant: SessionTenant | null,
): AuthSession {
  return {
    accessToken: response.accessToken,
    refreshToken: response.refreshToken,
    tenantId: tenant?.tenantId ?? null,
    tenantName: tenant?.name ?? null,
    tenantSlug: tenant?.slug ?? null,
    role: normalizeRole(tenant?.role),
    user: response.user,
  }
}
