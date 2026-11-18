import { apiRequest } from '@/lib/api-client'

export type AuthUser = {
  id: string
  email: string
  displayName: string
  isPlatformAdmin: boolean
  emailConfirmed: boolean
}

export type TenantLoginDto = {
  tenantId: string
  tenantName: string
  tenantSlug: string
  role: string
}

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
  user: AuthUser
  tenant: TenantLoginDto | null
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
  } | null
  tenant: {
    id: string
    name: string
    slug: string
    role: string
  }
  emailVerificationRequired: boolean
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

export function confirmEmail(email: string, token: string): Promise<AuthUser> {
  return apiRequest<AuthUser>('/auth/confirm-email', {
    method: 'POST',
    body: JSON.stringify({ email, token }),
  })
}

export function getCurrentUser(): Promise<AuthUser> {
  return apiRequest<AuthUser>('/auth/me')
}

export function logoutRequest(refreshToken: string): Promise<void> {
  return apiRequest<void>('/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  })
}

export function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  return apiRequest<void>('/auth/change-password', {
    method: 'POST',
    body: JSON.stringify({ currentPassword, newPassword }),
  })
}

export function forgotPassword(email: string): Promise<void> {
  return apiRequest<void>('/auth/forgot-password', {
    method: 'POST',
    body: JSON.stringify({ email }),
  })
}

export function resetPassword(email: string, token: string, newPassword: string): Promise<void> {
  return apiRequest<void>('/auth/reset-password', {
    method: 'POST',
    body: JSON.stringify({ email, token, newPassword }),
  })
}
