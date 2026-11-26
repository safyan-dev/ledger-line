import type { QueryClient } from '@tanstack/react-query'
import { qk } from '@/lib/query-keys'

// Which query roots a mutation invalidates. Declared once so adding an endpoint
// does not mean remembering four hand-written keys at every call site.
export type MutationResource = 'payment' | 'order' | 'member' | 'invitation' | 'ticket' | 'webhook' | 'subscription' | 'account' | 'tenant'

const resources: Record<MutationResource, readonly (readonly unknown[])[]> = {
  payment: [qk.paymentsRoot, qk.reconciliationRoot, qk.dashboardRoot, qk.ordersRoot, qk.refundsRoot, qk.auditRoot],
  order: [qk.ordersRoot, qk.paymentsRoot, qk.reconciliationRoot, qk.dashboardRoot, qk.auditRoot],
  member: [qk.teamRoot, qk.usageRoot, qk.tenantRoot, qk.auditRoot],
  invitation: [qk.teamRoot, qk.usageRoot, qk.auditRoot],
  ticket: [qk.supportRoot, qk.dashboardRoot],
  webhook: [qk.webhooksRoot],
  subscription: [qk.subscriptionRoot, qk.usageRoot, qk.teamRoot, qk.tenantRoot],
  account: [qk.accountRoot],
  tenant: [qk.tenantRoot, qk.auditRoot],
}

export function invalidateRelated(queryClient: QueryClient, ...kinds: MutationResource[]) {
  const roots = new Set<string>()
  for (const kind of kinds) {
    for (const root of resources[kind]) {
      queryClient.invalidateQueries({ queryKey: root })
    }
    for (const root of resources[kind]) roots.add(String(root[0]))
  }
  return roots
}
