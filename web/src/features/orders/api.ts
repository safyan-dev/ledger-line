import { apiRequest, toQueryString } from '@/lib/api-client'
import type { OrderDto, OrderListItemDto, PagedResult } from '@/types/api'

export type OrderFilters = {
  page: number
  pageSize: number
  status?: string
  search?: string
  sort?: string
}

export function getOrders(filters: OrderFilters) {
  return apiRequest<PagedResult<OrderListItemDto>>(`/orders${toQueryString(filters)}`)
}

export function getOrder(orderId: string) {
  return apiRequest<OrderDto>(`/orders/${orderId}`)
}

export type CreateOrderInput = {
  customerName: string
  customerReference?: string
  description?: string
  expectedAmount: number
  currency: string
  orderNumber?: string
}

export function createOrder(input: CreateOrderInput) {
  return apiRequest<OrderDto>('/orders', {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

export function cancelOrder(orderId: string, reason: string) {
  return apiRequest<OrderDto>(`/orders/${orderId}/cancel`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  })
}
