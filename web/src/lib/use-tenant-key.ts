import { useSession } from '@/lib/session'
import type { TenantId } from '@/lib/query-keys'

// The tenant scope for query keys. Null for platform-admin and public views,
// which read platform-level data rather than workspace data.
export function useTenantKey(): TenantId {
  return useSession()?.tenantId ?? null
}
