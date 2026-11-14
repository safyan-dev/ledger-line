import { apiRequest } from '@/lib/api-client'
import type { FeatureAvailabilityDto, UsageSummaryDto } from '@/types/api'

export function getUsage(): Promise<UsageSummaryDto> {
  return apiRequest<UsageSummaryDto>('/usage')
}

export function getFeatureAvailability(featureKey: string): Promise<FeatureAvailabilityDto> {
  return apiRequest<FeatureAvailabilityDto>(`/usage/features/${encodeURIComponent(featureKey)}`)
}
