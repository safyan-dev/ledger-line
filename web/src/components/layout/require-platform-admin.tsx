import { Link, Navigate } from 'react-router-dom'
import { AccessDenied } from '@/components/layout/access-denied'
import { useSession } from '@/lib/session'
import type { ReactNode } from 'react'

export function RequirePlatformAdmin({ children }: { children: ReactNode }) {
  const session = useSession()

  if (!session) {
    return <Navigate to="/sign-in" replace />
  }

  if (!session.user.isPlatformAdmin) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16">
        <AccessDenied
          title="Platform access restricted"
          description="Only platform administrators can open this area. Sign in to your workspace to continue."
        />
        <p className="mt-4 text-center text-xs text-muted-foreground">
          <Link to="/dashboard" className="font-medium text-primary hover:underline">
            Go to your workspace
          </Link>
        </p>
      </div>
    )
  }

  return <>{children}</>
}
