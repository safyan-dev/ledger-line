import { apiRequest } from '@/lib/api-client'

export type PlanFeature = {
  key: string
  label: string
  included: boolean
  note: string | null
}

export type PlanLimit = {
  key: string
  label: string
  value: number | null
  display: string
}

export type Plan = {
  code: string
  name: string
  tagline: string
  audience: string
  monthlyPrice: number | null
  priceNote: string
  recommended: boolean
  sortOrder: number
  features: PlanFeature[]
  limits: PlanLimit[]
}

export function fetchPlans(): Promise<Plan[]> {
  return apiRequest<Plan[]>('/plans')
}
