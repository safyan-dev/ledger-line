import { apiRequest, toQueryString } from '@/lib/api-client'
import type { PagedResult, PaymentDto, PaymentListItemDto, RefundDto } from '@/types/api'

export type PaymentFilters = {
  page: number
  pageSize: number
  status?: string
  provider?: string
  search?: string
  orderId?: string
}

export function getPayments(filters: PaymentFilters) {
  return apiRequest<PagedResult<PaymentListItemDto>>(`/payments${toQueryString(filters)}`)
}

export function getPayment(paymentId: string) {
  return apiRequest<PaymentDto>(`/payments/${paymentId}`)
}

export type RecordPaymentInput = {
  provider?: string
  providerPaymentId?: string
  amount: number
  currency: string
  customerName?: string
  customerReference?: string
  reference?: string
  receivedAt?: string
}

export function recordPayment(input: RecordPaymentInput) {
  return apiRequest<PaymentDto>('/payments', {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

export function rerunReconciliation(paymentId: string) {
  return apiRequest(`/payments/${paymentId}/reconcile`, { method: 'POST' })
}

export function refundPayment(paymentId: string, input: { amount: number; reason: string }) {
  return apiRequest<RefundDto>(`/payments/${paymentId}/refunds`, {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

export function getRefunds(paymentId: string) {
  return apiRequest<RefundDto[]>(`/payments/${paymentId}/refunds`)
}
