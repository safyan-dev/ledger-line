import { useSyncExternalStore } from 'react'
import type { AuthUser } from '@/lib/auth-api'

export type AuthSession = {
  accessToken: string
  refreshToken: string
  tenantSlug: string | null
  user: AuthUser
}

export type SelectedPlan = {
  code: string
  name: string
}

const SESSION_KEY = 'ledgerline.session'
const PLAN_KEY = 'ledgerline.selectedPlan'

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
      tenantSlug: parsed.tenantSlug ?? null,
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
