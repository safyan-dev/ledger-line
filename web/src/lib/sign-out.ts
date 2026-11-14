import type { QueryClient } from '@tanstack/react-query'
import { logoutRequest } from '@/lib/auth-api'
import { clearTenantStorage, loadSession } from '@/lib/session'

export async function signOut(queryClient: QueryClient): Promise<void> {
  const session = loadSession()

  if (session?.refreshToken) {
    try {
      await logoutRequest(session.refreshToken)
    } catch {
      // The session is cleared locally even if the server cannot revoke the token.
    }
  }

  queryClient.clear()
  clearTenantStorage()
}
