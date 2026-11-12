import { apiRequest, toQueryString } from '@/lib/api-client'
import type { AuditLogDto, PagedResult } from '@/types/api'

export type AuditFilters = {
  page: number
  pageSize: number
  module?: string
  action?: string
  entityType?: string
  entityId?: string
  search?: string
}

export function getAuditLogs(filters: AuditFilters) {
  return apiRequest<PagedResult<AuditLogDto>>(`/audit-logs${toQueryString(filters)}`)
}
