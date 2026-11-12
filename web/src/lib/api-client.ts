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

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      ...(init?.body ? { 'content-type': 'application/json' } : {}),
      ...init?.headers,
    },
  })

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
