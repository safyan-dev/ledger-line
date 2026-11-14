import { apiRequest, toQueryString } from '@/lib/api-client'
import type { PagedResult, SubscriptionDto, SubscriptionHistoryDto } from '@/types/api'

export { fetchPlans } from '@/public-site/api'
export type { Plan } from '@/public-site/api'

export function getSubscription(): Promise<SubscriptionDto> {
  return apiRequest<SubscriptionDto>('/tenants/current/subscription')
}

export function changeSubscriptionPlan(planCode: string): Promise<SubscriptionDto> {
  return apiRequest<SubscriptionDto>('/tenants/current/subscription', {
    method: 'PUT',
    body: JSON.stringify({ planCode }),
  })
}

export function cancelSubscription(): Promise<SubscriptionDto> {
  return apiRequest<SubscriptionDto>('/tenants/current/subscription/cancel', {
    method: 'POST',
  })
}

export function reactivateSubscription(): Promise<SubscriptionDto> {
  return apiRequest<SubscriptionDto>('/tenants/current/subscription/reactivate', {
    method: 'POST',
  })
}

export function getSubscriptionHistory(filters: {
  page: number
  pageSize: number
}): Promise<PagedResult<SubscriptionHistoryDto>> {
  return apiRequest<PagedResult<SubscriptionHistoryDto>>(
    `/tenants/current/subscription/history${toQueryString(filters)}`,
  )
}
