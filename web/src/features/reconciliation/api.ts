import { apiRequest, toQueryString } from '@/lib/api-client'
import type { PagedResult, ReconciliationRecordDto } from '@/types/api'

export type ReconciliationFilters = {
  page: number
  pageSize: number
  outcome?: string
  search?: string
  paymentId?: string
  orderId?: string
  from?: string
  to?: string
  reason?: string
}

export function getReconciliationRecords(filters: ReconciliationFilters) {
  return apiRequest<PagedResult<ReconciliationRecordDto>>(`/reconciliation${toQueryString(filters)}`)
}

export function getReconciliationForPayment(paymentId: string) {
  return apiRequest<ReconciliationRecordDto[]>(`/reconciliation/payment/${paymentId}`)
}

export function manualMatch(input: { paymentId: string; orderId: string; note?: string }) {
  return apiRequest<ReconciliationRecordDto>('/reconciliation/manual-match', {
    method: 'POST',
    body: JSON.stringify(input),
  })
}
