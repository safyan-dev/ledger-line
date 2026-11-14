import { apiRequest } from '@/lib/api-client'
import type { AcceptInvitationResponse } from '@/types/api'

export function acceptInvitation(input: {
  token: string
  displayName?: string
  password?: string
}): Promise<AcceptInvitationResponse> {
  return apiRequest<AcceptInvitationResponse>('/invitations/accept', {
    method: 'POST',
    body: JSON.stringify(input),
  })
}
