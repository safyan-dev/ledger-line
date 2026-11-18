import { apiRequest } from '@/lib/api-client'
import { toQueryString } from '@/lib/api-client'
import type {
  PlatformDashboardDto,
  PlatformPlanDto,
  PlatformSubscriptionDto,
  PlatformSupportTicketDetailDto,
  PlatformSupportTicketDto,
  PlatformTenantDetailsDto,
  PlatformTenantDto,
  PagedResult,
  SupportTicketDto,
  SupportTicketMessageDto,
} from '@/types/api'

export function getPlatformTenants(filters: {
  page: number
  pageSize: number
  search?: string
  status?: string
}): Promise<PagedResult<PlatformTenantDto>> {
  return apiRequest<PagedResult<PlatformTenantDto>>(
    `/platform/tenants${toQueryString(filters)}`,
  )
}

export function getPlatformDashboard(): Promise<PlatformDashboardDto> {
  return apiRequest<PlatformDashboardDto>('/platform/dashboard')
}

export function getPlatformTenant(tenantId: string): Promise<PlatformTenantDetailsDto> {
  return apiRequest<PlatformTenantDetailsDto>(`/platform/tenants/${tenantId}`)
}

export function updatePlatformTenantStatus(tenantId: string, status: 'Active' | 'Suspended') {
  return apiRequest<{ id: string; status: string }>(`/platform/tenants/${tenantId}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  })
}

export function getPlatformPlans(): Promise<PlatformPlanDto[]> {
  return apiRequest<PlatformPlanDto[]>('/platform/plans')
}

export function updatePlatformPlanStatus(code: string, isActive: boolean) {
  return apiRequest<{ code: string; isActive: boolean }>(`/platform/plans/${encodeURIComponent(code)}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ isActive }),
  })
}

export function updatePlatformPlanFeature(code: string, featureKey: string, included: boolean) {
  return apiRequest<{ key: string; isIncluded: boolean }>(
    `/platform/plans/${encodeURIComponent(code)}/features/${encodeURIComponent(featureKey)}`,
    {
      method: 'PATCH',
      body: JSON.stringify({ included }),
    },
  )
}

export function updatePlatformPlanLimit(code: string, limitKey: string, value: number | null) {
  return apiRequest<{ key: string; label: string; value: number | null; display: string }>(
    `/platform/plans/${encodeURIComponent(code)}/limits/${encodeURIComponent(limitKey)}`,
    {
      method: 'PATCH',
      body: JSON.stringify({ value }),
    },
  )
}

export function getPlatformSubscriptions(filters: {
  page: number
  pageSize: number
  status?: string
}): Promise<PagedResult<PlatformSubscriptionDto>> {
  return apiRequest<PagedResult<PlatformSubscriptionDto>>(
    `/platform/subscriptions${toQueryString(filters)}`,
  )
}

export function getPlatformSubscription(id: string): Promise<PlatformSubscriptionDto> {
  return apiRequest<PlatformSubscriptionDto>(`/platform/subscriptions/${id}`)
}

export function activatePlatformSubscription(id: string): Promise<PlatformSubscriptionDto> {
  return apiRequest<PlatformSubscriptionDto>(`/platform/subscriptions/${id}/activate`, { method: 'POST' })
}

export function changePlatformSubscriptionPlan(id: string, planCode: string): Promise<PlatformSubscriptionDto> {
  return apiRequest<PlatformSubscriptionDto>(`/platform/subscriptions/${id}/plan`, {
    method: 'PUT',
    body: JSON.stringify({ planCode }),
  })
}

export function extendPlatformSubscription(id: string, days: number): Promise<PlatformSubscriptionDto> {
  return apiRequest<PlatformSubscriptionDto>(`/platform/subscriptions/${id}/extend`, {
    method: 'POST',
    body: JSON.stringify({ days }),
  })
}

export function updatePlatformSubscriptionStatus(
  id: string,
  status: 'Active' | 'Suspended',
): Promise<PlatformSubscriptionDto> {
  return apiRequest<PlatformSubscriptionDto>(`/platform/subscriptions/${id}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  })
}

export function getPlatformSupportTickets(filters: {
  page: number
  pageSize: number
  status?: string
  category?: string
  priority?: string
  search?: string
}): Promise<PagedResult<PlatformSupportTicketDto>> {
  return apiRequest<PagedResult<PlatformSupportTicketDto>>(
    `/platform/support/tickets${toQueryString(filters)}`,
  )
}

export function getPlatformSupportTicket(id: string): Promise<PlatformSupportTicketDetailDto> {
  return apiRequest<PlatformSupportTicketDetailDto>(`/platform/support/tickets/${id}`)
}

export function replyToPlatformSupportTicket(id: string, body: string) {
  return apiRequest<SupportTicketMessageDto>(`/platform/support/tickets/${id}/messages`, {
    method: 'POST',
    body: JSON.stringify({ body }),
  })
}

export function updatePlatformSupportTicketStatus(id: string, status: string) {
  return apiRequest<SupportTicketDto>(`/platform/support/tickets/${id}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  })
}
