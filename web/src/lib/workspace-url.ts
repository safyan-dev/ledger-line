import type { AuthSession } from '@/lib/session'
import { encodeHandoff } from '@/lib/session-handoff'

// The workspace is identified by the first host label, and the API resolves it
// from the Host header before any session value. So a session that claims tenant
// `beta` while the browser sits on `acme.localhost` is not enough — the reload
// must land on the new subdomain, or every request stays scoped to `acme`.

// The root domain is configuration, not something to guess: splitting "the last
// two labels" turns the apex `example.com` into `beta.com`, and "everything
// after the first label" turns `acme.example.com` into `beta.example.com` only by
// luck. VITE_ROOT_DOMAIN states it outright; without it we assume the common
// `<tenant>.<root>` shape of local development.
const ROOT_DOMAIN = (import.meta.env.VITE_ROOT_DOMAIN as string | undefined)?.toLowerCase() ?? null

function rootDomainFor(hostname: string): string | null {
  if (ROOT_DOMAIN) return ROOT_DOMAIN

  // Development default: `<tenant>.localhost`. Also covers bare localhost.
  if (hostname === 'localhost' || hostname.endsWith('.localhost')) return 'localhost'

  // No configured root and a public suffix we cannot resolve without the public
  // suffix list: refuse to rewrite rather than send the user to a wrong origin.
  return null
}

// Returns a URL only when the host can actually carry a workspace subdomain.
export function workspaceUrl(
  tenantSlug: string,
  path = '/dashboard',
  session?: AuthSession | null,
): string | null {
  if (typeof window === 'undefined') return null

  const { protocol, hostname, port } = window.location
  const host = hostname.toLowerCase()
  const root = rootDomainFor(host)
  if (!root) return null

  // Only rewrite hosts that belong to the configured root domain.
  const isApex = host === root
  const isSubdomain = host.endsWith(`.${root}`)
  if (!isApex && !isSubdomain) return null

  // Already on the target workspace: nothing to hand over.
  const currentSlug = isSubdomain ? host.slice(0, -(root.length + 1)).split('.').pop() : null
  const stayingPut = currentSlug === tenantSlug.toLowerCase()

  const origin = `${protocol}//${tenantSlug}.${root}${port ? `:${port}` : ''}`

  // localStorage is per origin, so a cross-workspace hop needs the session to
  // travel with it. The fragment never reaches the server.
  const hash = session && !stayingPut ? `#session-handoff=${encodeHandoff(session)}` : ''

  return `${origin}${path}${hash}`
}

// Full navigation is required: a client-side route change keeps the current
// origin, and the origin is exactly what identifies the workspace.
export function goToWorkspace(
  tenantSlug: string,
  path = '/dashboard',
  session?: AuthSession | null,
): boolean {
  const target = workspaceUrl(tenantSlug, path, session)
  if (!target) return false
  window.location.assign(target)
  return true
}
