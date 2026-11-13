import { apiRequest } from '@/lib/api-client'
import type { AuthSession } from '@/lib/session'

export type AuthUser = {
  id: string
  email: string
  displayName: string
  isPlatformAdmin: boolean
}

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
  user: AuthUser
}

export type Membership = {
  tenantId: string
  name: string
  slug: string
  role: string
}

export type RegisterTenantPayload = {
  companyName: string
  tenantSlug: string
  ownerName: string
  email: string
  password: string
  planCode?: string
}

export type RegisterTenantResponse = {
  session: {
    accessToken: string
    refreshToken: string
    accessTokenExpiresAt: string
    refreshTokenExpiresAt: string
    user: AuthUser
  }
  tenant: {
    id: string
    name: string
    slug: string
    role: string
  }
}

export function loginRequest(email: string, password: string): Promise<AuthResponse> {
  return apiRequest<AuthResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
}

export function fetchMemberships(): Promise<Membership[]> {
  return apiRequest<Membership[]>('/tenants/mine')
}

export function registerTenantRequest(payload: RegisterTenantPayload): Promise<RegisterTenantResponse> {
  return apiRequest<RegisterTenantResponse>('/tenants/register', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function sessionFromAuth(response: AuthResponse, tenantSlug: string | null): AuthSession {
  return {
    accessToken: response.accessToken,
    refreshToken: response.refreshToken,
    tenantSlug,
    user: response.user,
  }
}
