import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { Skeleton } from '@/components/ui/skeleton'
import { AccessDenied } from '@/components/layout/access-denied'
import { usePermissions } from '@/lib/permissions'
import { useSession } from '@/lib/session'
import type { TenantRole } from '@/lib/roles'

export function RequireRole({
  allow,
  children,
  title,
  description,
}: {
  allow: TenantRole[]
  children: ReactNode
  title?: string
  description?: string
}) {
  const session = useSession()
  const { role } = usePermissions()

  if (!session) {
    return <Navigate to="/sign-in" replace />
  }

  if (!role) {
    return (
      <div className="space-y-4" aria-busy="true" aria-label="Checking access">
        <Skeleton className="h-8 w-56" />
        <Skeleton className="h-40 w-full rounded-xl" />
      </div>
    )
  }

  if (!allow.includes(role)) {
    return <AccessDenied title={title} description={description} />
  }

  return <>{children}</>
}
