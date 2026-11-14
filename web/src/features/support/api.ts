import { apiRequest, toQueryString } from '@/lib/api-client'
import type {
  PagedResult,
  SupportTicketDetailDto,
  SupportTicketDto,
  SupportTicketMessageDto,
} from '@/types/api'

export type SupportTicketFilters = {
  page: number
  pageSize: number
  status?: string
  category?: string
  priority?: string
}

export function getTickets(filters: SupportTicketFilters): Promise<PagedResult<SupportTicketDto>> {
  return apiRequest<PagedResult<SupportTicketDto>>(`/support/tickets${toQueryString(filters)}`)
}

export function getTicket(ticketId: string): Promise<SupportTicketDetailDto> {
  return apiRequest<SupportTicketDetailDto>(`/support/tickets/${ticketId}`)
}

export type CreateTicketInput = {
  subject: string
  description: string
  category: string
  priority: string
}

export function createTicket(input: CreateTicketInput): Promise<SupportTicketDto> {
  return apiRequest<SupportTicketDto>('/support/tickets', {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

export function replyToTicket(ticketId: string, body: string): Promise<SupportTicketMessageDto> {
  return apiRequest<SupportTicketMessageDto>(`/support/tickets/${ticketId}/messages`, {
    method: 'POST',
    body: JSON.stringify({ body }),
  })
}

export function changeTicketStatus(ticketId: string, status: string): Promise<SupportTicketDto> {
  return apiRequest<SupportTicketDto>(`/support/tickets/${ticketId}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  })
}
