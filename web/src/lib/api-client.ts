import { clearSession, loadSession, saveSession, type AuthSession } from '@/lib/session'
import type { AuthResponse, AuthUser } from '@/lib/auth-api'
import type { ProblemDetails } from '@/types/api'

const API_BASE = import.meta.env.VITE_API_URL ?? '/api'

export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly failures?: Record<string, string[]>

  constructor(message: string, status: number, code?: string, failures?: Record<string, string[]>) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.failures = failures
  }
}

type QueryValue = string | number | boolean | undefined | null

export function toQueryString(params: Record<string, QueryValue>): string {
  const search = new URLSearchParams()

  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue
    search.set(key, String(value))
  }

  const query = search.toString()
  return query ? `?${query}` : ''
}

function buildHeaders(init: RequestInit | undefined, session: AuthSession | null): Headers {
  const headers = new Headers(init?.headers)

  if (init?.body && !headers.has('content-type')) {
    headers.set('content-type', 'application/json')
  }

  if (session) {
    headers.set('Authorization', `Bearer ${session.accessToken}`)
    if (session.tenantSlug) {
      headers.set('X-Tenant-Slug', session.tenantSlug)
    }
  }

  return headers
}

function send(path: string, init: RequestInit | undefined, session: AuthSession | null): Promise<Response> {
  return fetch(`${API_BASE}${path}`, { ...init, headers: buildHeaders(init, session) })
}

async function refreshSession(session: AuthSession): Promise<AuthSession | null> {
  try {
    const response = await fetch(`${API_BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    })

    if (!response.ok) {
      return null
    }

    const body = (await response.json()) as AuthResponse
    const next: AuthSession = {
      accessToken: body.accessToken,
      refreshToken: body.refreshToken,
      tenantId: session.tenantId,
      tenantName: session.tenantName,
      tenantSlug: session.tenantSlug,
      role: session.role,
      user: (body.user ?? session.user) as AuthUser,
    }
    saveSession(next)
    return next
  } catch {
    return null
  }
}

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  let session = loadSession()
  let response = await send(path, init, session)

  if (response.status === 401 && session && !path.startsWith('/auth/')) {
    const refreshed = await refreshSession(session)
    if (!refreshed) {
      clearSession()
      throw new ApiError('Your session has expired. Sign in again.', 401, 'auth.session_expired')
    }

    session = refreshed
    response = await send(path, init, session)
  }

  if (!response.ok) {
    let problem: ProblemDetails | undefined
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      problem = undefined
    }

    throw new ApiError(
      problem?.detail ?? problem?.title ?? response.statusText,
      response.status,
      problem?.code,
      problem?.errors,
    )
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof TypeError) {
    return 'The API could not be reached. Check that the backend is running and try again.'
  }
  if (error instanceof Error) return error.message
  return 'Something went wrong. Try again.'
}
