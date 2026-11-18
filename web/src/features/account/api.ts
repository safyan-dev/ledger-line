import { apiRequest } from '@/lib/api-client'
import type { AuthUser } from '@/lib/auth-api'

export function getProfile(): Promise<AuthUser> {
  return apiRequest<AuthUser>('/account/profile')
}

export function updateProfile(input: { displayName: string }): Promise<AuthUser> {
  return apiRequest<AuthUser>('/account/profile', {
    method: 'PATCH',
    body: JSON.stringify(input),
  })
}

export function requestEmailChange(newEmail: string): Promise<void> {
  return apiRequest<void>('/account/change-email', {
    method: 'POST',
    body: JSON.stringify({ newEmail }),
  })
}

export function confirmEmailChange(newEmail: string, token: string): Promise<AuthUser> {
  return apiRequest<AuthUser>('/account/change-email/confirm', {
    method: 'POST',
    body: JSON.stringify({ newEmail, token }),
  })
}
