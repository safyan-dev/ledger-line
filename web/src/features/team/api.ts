import { apiRequest, toQueryString } from '@/lib/api-client'
import type {
  IssuedTenantInvitationDto,
  PagedResult,
  TenantInvitationDto,
  TenantMemberDto,
} from '@/types/api'

export type InvitationFilters = {
  page: number
  pageSize: number
  status?: string
}

export function getMembers(): Promise<TenantMemberDto[]> {
  return apiRequest<TenantMemberDto[]>('/tenants/current/members')
}

export function inviteMember(input: { email: string; role: string }): Promise<IssuedTenantInvitationDto> {
  return apiRequest<IssuedTenantInvitationDto>('/tenants/current/invitations', {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

export function getInvitations(filters: InvitationFilters): Promise<PagedResult<TenantInvitationDto>> {
  return apiRequest<PagedResult<TenantInvitationDto>>(
    `/tenants/current/invitations${toQueryString(filters)}`,
  )
}

export function resendInvitation(invitationId: string): Promise<IssuedTenantInvitationDto> {
  return apiRequest<IssuedTenantInvitationDto>(`/tenants/current/invitations/${invitationId}/resend`, {
    method: 'POST',
  })
}

export function revokeInvitation(invitationId: string): Promise<TenantInvitationDto> {
  return apiRequest<TenantInvitationDto>(`/tenants/current/invitations/${invitationId}/revoke`, {
    method: 'POST',
  })
}

export function changeMemberRole(userId: string, role: string): Promise<TenantMemberDto> {
  return apiRequest<TenantMemberDto>(`/tenants/current/members/${userId}`, {
    method: 'PATCH',
    body: JSON.stringify({ role }),
  })
}

export function removeMember(userId: string): Promise<void> {
  return apiRequest<void>(`/tenants/current/members/${userId}`, { method: 'DELETE' })
}
