// Query keys carry the tenant id, so a cache entry can never be read by a
// different workspace. `RequireSession` additionally clears the cache on tenant
// change to bound memory; this file makes the isolation structural.
//
// Usage:  queryKey: qk.payments(tenantId, filters)
//         invalidate: queryClient.invalidateQueries({ queryKey: qk.paymentsRoot })
//
// Invalidation uses the root key (['payments']) so it matches every tenant scope.

export type TenantId = string | null

const scoped = <T extends readonly unknown[]>(root: string, tenantId: TenantId, rest: T) =>
  [root, tenantId ?? 'anonymous', ...rest] as const

export const qk = {
  // roots — use these for invalidation
  paymentsRoot: ['payments'] as const,
  ordersRoot: ['orders'] as const,
  reconciliationRoot: ['reconciliation'] as const,
  webhooksRoot: ['webhooks'] as const,
  auditRoot: ['audit'] as const,
  teamRoot: ['team'] as const,
  supportRoot: ['support'] as const,
  subscriptionRoot: ['subscription'] as const,
  usageRoot: ['usage'] as const,
  tenantRoot: ['tenant'] as const,
  accountRoot: ['account'] as const,
  dashboardRoot: ['dashboard'] as const,
  refundsRoot: ['refunds'] as const,

  // tenant-scoped entries
  payments: <T extends Record<string, unknown>>(tenantId: TenantId, filters: T) =>
    scoped('payments', tenantId, [filters] as const),
  payment: (tenantId: TenantId, paymentId: string | null | undefined) => scoped('payments', tenantId, [paymentId] as const),
  paymentsForOrder: (tenantId: TenantId, orderId: string | null | undefined) =>
    scoped('payments', tenantId, ['order', orderId] as const),

  orders: <T extends Record<string, unknown>>(tenantId: TenantId, filters: T) =>
    scoped('orders', tenantId, [filters] as const),
  order: (tenantId: TenantId, orderId: string | null | undefined) => scoped('orders', tenantId, [orderId] as const),

  reconciliation: <T extends Record<string, unknown>>(tenantId: TenantId, filters: T) =>
    scoped('reconciliation', tenantId, [filters] as const),
  reconciliationForPayment: (tenantId: TenantId, paymentId: string | null | undefined) =>
    scoped('reconciliation', tenantId, ['payment', paymentId] as const),
  reconciliationForOrder: (tenantId: TenantId, orderId: string | null | undefined) =>
    scoped('reconciliation', tenantId, ['order', orderId] as const),

  webhooks: <T extends Record<string, unknown>>(tenantId: TenantId, filters: T) =>
    scoped('webhooks', tenantId, [filters] as const),
  webhook: (tenantId: TenantId, eventId: string | null | undefined) => scoped('webhooks', tenantId, [eventId] as const),

  audit: <T extends Record<string, unknown>>(tenantId: TenantId, filters: T) =>
    scoped('audit', tenantId, [filters] as const),
  auditFor: (tenantId: TenantId, entity: 'order' | 'payment', entityId: string | null | undefined) =>
    scoped('audit', tenantId, [entity, entityId] as const),

  teamMembers: (tenantId: TenantId) => scoped('team', tenantId, ['members'] as const),
  teamInvitations: <T extends Record<string, unknown>>(tenantId: TenantId, filters: T) =>
    scoped('team', tenantId, ['invitations', filters] as const),

  supportTickets: <T extends Record<string, unknown>>(tenantId: TenantId, filters: T) =>
    scoped('support', tenantId, ['tickets', filters] as const),
  supportTicket: (tenantId: TenantId, ticketId: string | null | undefined) =>
    scoped('support', tenantId, ['ticket', ticketId] as const),

  subscription: (tenantId: TenantId) => scoped('subscription', tenantId, ['current'] as const),
  subscriptionHistory: <T extends Record<string, unknown>>(tenantId: TenantId, filters: T) =>
    scoped('subscription', tenantId, ['history', filters] as const),
  usage: (tenantId: TenantId) => scoped('usage', tenantId, []),
  tenantCurrent: (tenantId: TenantId) => scoped('tenant', tenantId, ['current'] as const),
  accountProfile: (tenantId: TenantId) => scoped('account', tenantId, ['profile'] as const),
  refunds: (tenantId: TenantId, paymentId: string | null | undefined) => scoped('refunds', tenantId, [paymentId] as const),

  dashboard: (tenantId: TenantId) => scoped('dashboard', tenantId, []),
}
