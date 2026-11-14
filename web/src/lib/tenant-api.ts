import { apiRequest } from '@/lib/api-client'
import type { TenantSettingsDto } from '@/types/api'

export function getCurrentTenant(): Promise<TenantSettingsDto> {
  return apiRequest<TenantSettingsDto>('/tenants/current')
}

export type UpdateTenantSettingsInput = {
  name?: string
  businessContactEmail?: string
  defaultCurrency?: string
  timeZone?: string
}

export function updateTenantSettings(input: UpdateTenantSettingsInput): Promise<TenantSettingsDto> {
  return apiRequest<TenantSettingsDto>('/tenants/current', {
    method: 'PUT',
    body: JSON.stringify(input),
  })
}
