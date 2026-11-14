import { useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { normalizeRole } from '@/lib/roles'
import { updateSessionTenant, useSession } from '@/lib/session'
import { getCurrentTenant } from '@/lib/tenant-api'

export function useTenantSessionSync() {
  const session = useSession()
  const query = useQuery({
    queryKey: ['tenant', 'current'],
    queryFn: getCurrentTenant,
    enabled: Boolean(session?.tenantSlug),
    staleTime: 60_000,
  })

  useEffect(() => {
    if (!session || !query.data) return

    const role = normalizeRole(query.data.role)
    updateSessionTenant({
      tenantId: query.data.id,
      tenantName: query.data.name,
      tenantSlug: query.data.slug,
      role,
    })
  }, [session, query.data])

  return query
}
