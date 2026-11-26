import { loadSession, saveSession, type AuthSession } from '@/lib/session'

// Sessions live in localStorage, which is scoped to one origin. Every workspace
// is its own origin (beta.localhost vs acme.localhost), so switching workspace
// cannot inherit the session — it has to be handed across explicitly.
//
// The payload travels in the URL fragment, which browsers never send to the
// server, and carries a short expiry so an old link cannot be replayed later.
// The receiving page strips the fragment immediately after reading it.

const PARAM = 'session-handoff'
const MAX_AGE_MS = 60_000

type Handoff = {
  exp: number
  session: AuthSession
}

function encodeBase64Url(value: string): string {
  const bytes = new TextEncoder().encode(value)
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function decodeBase64Url(value: string): string | null {
  try {
    const padded = value.replace(/-/g, '+').replace(/_/g, '/')
    const binary = atob(padded.padEnd(Math.ceil(padded.length / 4) * 4, '='))
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
    return new TextDecoder().decode(bytes)
  } catch {
    return null
  }
}

export function encodeHandoff(session: AuthSession): string {
  const payload: Handoff = { exp: Date.now() + MAX_AGE_MS, session }
  return encodeBase64Url(JSON.stringify(payload))
}

// Reads and consumes a handoff fragment on the target origin. Returns true when
// a session was restored, so the caller can decide whether to re-render.
export function consumeHandoff(): boolean {
  if (typeof window === 'undefined') return false

  const hash = window.location.hash.replace(/^#/, '')
  if (!hash) return false

  const params = new URLSearchParams(hash)
  const raw = params.get(PARAM)
  if (!raw) return false

  // Strip the fragment before doing anything else: it must not survive a reload
  // or end up in history as a reusable credential.
  window.history.replaceState(
    null,
    '',
    `${window.location.pathname}${window.location.search}`,
  )

  const json = decodeBase64Url(raw)
  if (!json) return false

  let payload: Handoff
  try {
    payload = JSON.parse(json) as Handoff
  } catch {
    return false
  }

  if (!payload?.session?.accessToken || !payload.session.user) return false
  if (typeof payload.exp !== 'number' || Date.now() > payload.exp) return false

  saveSession(payload.session)
  return true
}

// True when the browser is already on the workspace's own origin.
export function isOnWorkspace(tenantSlug: string | null | undefined): boolean {
  if (typeof window === 'undefined' || !tenantSlug) return false
  const labels = window.location.hostname.toLowerCase().split('.').filter(Boolean)
  if (labels.length < 2) return false
  return labels[0] === tenantSlug.toLowerCase()
}

export function currentSession(): AuthSession | null {
  return loadSession()
}
