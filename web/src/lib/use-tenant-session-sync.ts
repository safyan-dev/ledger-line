import { useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { normalizeRole } from '@/lib/roles'
import { updateSessionTenant, useSession } from '@/lib/session'
import { getCurrentTenant } from '@/lib/tenant-api'

// The workspace is identified by its slug, so the key is scoped to it rather
// than to the tenant id this query is responsible for discovering.
function tenantSlugKey(slug: string | null | undefined) {
  return ['tenant', 'current', slug ?? 'none'] as const
}

export function useTenantSessionSync() {
  const session = useSession()
  const slug = session?.tenantSlug ?? null

  const query = useQuery({
    queryKey: tenantSlugKey(slug),
    queryFn: getCurrentTenant,
    enabled: Boolean(slug),
    staleTime: 60_000,
  })

  const tenant = query.data

  // Depend on the resolved tenant only. Depending on `session` re-ran this on
  // every unrelated session change (display name, tokens) for no effect.
  useEffect(() => {
    if (!tenant) return

    updateSessionTenant({
      tenantId: tenant.id,
      tenantName: tenant.name,
      tenantSlug: tenant.slug,
      role: normalizeRole(tenant.role),
    })
  }, [tenant])

  return query
}
