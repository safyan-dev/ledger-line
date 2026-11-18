import type { AuthUser } from '@/lib/auth-api'

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type OrderDto = {
  id: string
  orderNumber: string
  customerName: string
  customerReference: string | null
  description: string | null
  expectedAmount: number
  paidAmount: number
  currency: string
  status: string
  createdAt: string
  updatedAt: string
  remainingAmount: number
}

export type OrderListItemDto = {
  id: string
  orderNumber: string
  customerName: string
  customerReference: string | null
  expectedAmount: number
  paidAmount: number
  currency: string
  status: string
  createdAt: string
}

export type PaymentDto = {
  id: string
  provider: string
  providerPaymentId: string
  orderId: string | null
  providerOrderId: string | null
  orderNumber: string | null
  amount: number
  refundedAmount: number
  refundableAmount: number
  currency: string
  status: string
  customerName: string | null
  customerReference: string | null
  reference: string | null
  failureReason: string | null
  receivedAt: string
  createdAt: string
  updatedAt: string
}

export type PaymentListItemDto = {
  id: string
  provider: string
  providerPaymentId: string
  orderId: string | null
  orderNumber: string | null
  amount: number
  refundedAmount: number
  currency: string
  status: string
  customerName: string | null
  customerReference: string | null
  reference: string | null
  receivedAt: string
}

export type RefundDto = {
  id: string
  paymentId: string
  orderId: string | null
  amount: number
  currency: string
  reason: string
  status: string
  provider: string
  providerRefundId: string
  createdBy: string
  createdAt: string
}

export type WebhookEventListItemDto = {
  id: string
  provider: string
  providerEventId: string
  eventType: string
  status: string
  paymentId: string | null
  failureReason: string | null
  attemptCount: number
  duplicateCount: number
  conflictCount: number
  lastConflictReason: string | null
  receivedAt: string
  processedAt: string | null
}

export type WebhookEventDto = WebhookEventListItemDto & {
  payload: string
  hasSignature: boolean
}

export type WebhookIngestionResult = {
  webhookEventId: string
  status: string
  isDuplicate: boolean
  paymentId: string | null
}

export type ReconciliationRecordDto = {
  id: string
  paymentId: string
  providerPaymentId: string | null
  orderId: string | null
  orderNumber: string | null
  outcome: string
  strategy: string
  expectedAmount: number | null
  actualAmount: number
  difference: number
  currency: string
  reason: string | null
  notes: string | null
  matchedBy: string
  createdAt: string
}

export type AuditLogDto = {
  id: string
  actorId: string | null
  actorName: string
  actorRole: string
  action: string
  module: string
  entityType: string
  entityId: string | null
  summary: string
  metadata: string | null
  correlationId: string | null
  occurredAt: string
}

export type DashboardKpis = {
  matchedRate7d: number
  unmatchedAmount: number
  paymentsToday: number
  pendingReview: number
}

export type DashboardFlow = {
  ordersCreated: number
  webhooksReceived: number
  paymentsRecorded: number
  successfulPayments: number
  matched: number
  unmatched: number
  manualMatches: number
}

export type VolumePoint = {
  date: string
  matchedAmount: number
  unmatchedAmount: number
  manualAmount: number
}

export type ReviewQueueItem = {
  paymentId: string
  providerPaymentId: string
  amount: number
  currency: string
  customerName: string | null
  reference: string | null
  receivedAt: string
  ageHours: number
}

export type ActivityItem = {
  id: string
  actorName: string
  action: string
  module: string
  entityType: string
  entityId: string | null
  summary: string
  occurredAt: string
}

export type DashboardSummary = {
  kpis: DashboardKpis
  flow: DashboardFlow
  volume: VolumePoint[]
  reviewQueue: ReviewQueueItem[]
  recentActivity: ActivityItem[]
}

export type TenantSettingsDto = {
  id: string
  name: string
  slug: string
  status: string
  role: string
  memberCount: number
  businessContactEmail: string | null
  defaultCurrency: string
  timeZone: string
  createdAt: string
  updatedAt: string
}

export type PlatformTenantDto = {
  id: string
  name: string
  slug: string
  status: string
  memberCount: number
  planCode: string | null
  planName: string | null
  createdAt: string
  ownerEmail: string | null
}

export type PlatformTenantOwnerDto = {
  id: string
  name: string
  email: string
}

export type PlatformTenantDetailsDto = {
  id: string
  name: string
  slug: string
  status: string
  businessContactEmail: string | null
  defaultCurrency: string
  timeZone: string
  createdAt: string
  memberCount: number
  owner: PlatformTenantOwnerDto | null
  planCode: string | null
  planName: string | null
  subscriptionStatus: string | null
  usage: UsageSummaryDto | null
}

export type PlatformPlanDto = {
  code: string
  name: string
  tagline: string
  audience: string
  monthlyPrice: number | null
  priceNote: string
  recommended: boolean
  sortOrder: number
  isActive: boolean
  features: { key: string; label: string; included: boolean; note: string | null }[]
  limits: { key: string; label: string; value: number | null; display: string }[]
}

export type PlatformSubscriptionDto = {
  id: string
  tenantId: string
  tenantName: string
  planCode: string
  planName: string
  status: string
  activationSource: string
  paymentConfirmed: boolean
  startDate: string
  currentPeriodStart: string
  currentPeriodEnd: string | null
  createdAt: string
}

export type PlatformSupportTicketDto = {
  id: string
  tenantId: string
  tenantName: string
  subject: string
  category: string
  priority: string
  status: string
  createdBy: string
  createdAt: string
}

export type PlatformSupportTicketDetailDto = {
  tenantId: string
  tenantName: string
  ticket: SupportTicketDto
  messages: SupportTicketMessageDto[]
}

export type PlatformRecentTenantDto = {
  id: string
  name: string
  ownerEmail: string | null
  planName: string | null
  createdAt: string
}

export type PlatformAttentionSubscriptionDto = {
  id: string
  tenantId: string
  tenantName: string
  planName: string
  status: string
  currentPeriodEnd: string | null
}

export type PlatformDashboardDto = {
  totalTenants: number
  activeTenants: number
  suspendedTenants: number
  activeSubscriptions: number
  trialSubscriptions: number
  recentRegistrations: PlatformRecentTenantDto[]
  subscriptionsRequiringAttention: PlatformAttentionSubscriptionDto[]
}

export type TenantMemberDto = {
  userId: string
  email: string
  displayName: string
  role: string
  joinedAt: string
}

export type InvitationStatus = 'Pending' | 'Accepted' | 'Expired' | 'Revoked'

export type TenantInvitationDto = {
  id: string
  email: string
  role: string
  status: InvitationStatus
  expiresAt: string
  createdAt: string
  lastSentAt: string
}

export type IssuedTenantInvitationDto = {
  invitation: TenantInvitationDto
}

export type InvitationSessionDto = {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
  user: AuthUser
}

export type AcceptInvitationResponse = {
  tenantId: string
  tenantSlug: string
  tenantName: string
  role: string
  session: InvitationSessionDto | null
}

export type SubscriptionDto = {
  id: string
  planCode: string
  planName: string
  status: string
  activationSource: string
  paymentConfirmed: boolean
  startDate: string
  currentPeriodStart: string
  currentPeriodEnd: string | null
  cancelAtPeriodEnd: boolean
  pendingPlanCode: string | null
  pendingPlanEffectiveAt: string | null
}

export type SubscriptionHistoryDto = {
  id: string
  event: string
  status: string
  planCode: string
  planName: string
  previousPlanCode: string | null
  note: string | null
  occurredAt: string
}

export type AccessFeatureDto = {
  key: string
  label: string
  included: boolean
  note: string | null
}

export type UsageMetricDto = {
  key: string
  label: string
  used: number
  limit: number | null
  remaining: number | null
  period: string
}

export type UsageSummaryDto = {
  planCode: string
  planName: string
  status: string
  periodStart: string
  periodEnd: string
  metrics: UsageMetricDto[]
  features: AccessFeatureDto[]
}

export type FeatureAvailabilityDto = {
  key: string
  enabled: boolean
}

export type SupportTicketDto = {
  id: string
  subject: string
  description: string
  category: string
  priority: string
  status: string
  createdByUserId: string
  createdBy: string
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
  closedAt: string | null
}

export type SupportTicketMessageDto = {
  id: string
  authorUserId: string
  authorName: string
  authorRole: string
  body: string
  isInternal: boolean
  createdAt: string
}

export type SupportTicketDetailDto = {
  ticket: SupportTicketDto
  messages: SupportTicketMessageDto[]
}

export type ProblemDetails = {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: string
  errors?: Record<string, string[]>
}
