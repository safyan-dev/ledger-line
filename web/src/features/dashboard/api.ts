import { apiRequest } from '@/lib/api-client'
import type { DashboardSummary } from '@/types/api'

export function getDashboardSummary() {
  return apiRequest<DashboardSummary>('/dashboard/summary')
}
