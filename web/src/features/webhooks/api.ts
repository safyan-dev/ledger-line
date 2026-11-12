import { apiRequest, toQueryString } from '@/lib/api-client'
import type { PagedResult, WebhookEventDto, WebhookEventListItemDto } from '@/types/api'

export type WebhookFilters = {
  page: number
  pageSize: number
  status?: string
  eventType?: string
  provider?: string
  search?: string
  paymentId?: string
}

export function getWebhookEvents(filters: WebhookFilters) {
  return apiRequest<PagedResult<WebhookEventListItemDto>>(`/webhooks${toQueryString(filters)}`)
}

export function getWebhookEvent(eventId: string) {
  return apiRequest<WebhookEventDto>(`/webhooks/${eventId}`)
}
