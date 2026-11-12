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

export type ProblemDetails = {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: string
  errors?: Record<string, string[]>
}
