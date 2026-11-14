import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Mail, RefreshCw, ShieldX, UserMinus, Users } from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, type DataTableColumn } from '@/components/data/data-table'
import { EmptyState } from '@/components/data/empty-state'
import { ErrorState } from '@/components/data/error-state'
import { ListPagination } from '@/components/data/list-pagination'
import { StatusBadge } from '@/components/data/status-badge'
import { ConfirmDialog } from '@/components/data/confirm-dialog'
import { CopyButton } from '@/components/data/copy-button'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { InviteMemberDialog } from '@/features/team/components/invite-member-dialog'
import { ChangeRoleDialog } from '@/features/team/components/change-role-dialog'
import {
  getInvitations,
  getMembers,
  removeMember,
  resendInvitation,
  revokeInvitation,
} from '@/features/team/api'
import { errorMessage } from '@/lib/api-client'
import { formatDate } from '@/lib/format'
import { usePermissions } from '@/lib/permissions'
import { roleLabel } from '@/lib/roles'
import {
  invitationStatusMeta,
  tenantRoleMeta,
} from '@/lib/status'
import { useTableParams } from '@/lib/use-table-params'
import type { TenantInvitationDto, TenantMemberDto } from '@/types/api'

export function TeamPage() {
  const { role } = usePermissions()
  const queryClient = useQueryClient()
  const { page, pageSize, get, update, setPage } = useTableParams()
  const status = get('status')

  const [roleTarget, setRoleTarget] = useState<TenantMemberDto | null>(null)
  const [removeTarget, setRemoveTarget] = useState<TenantMemberDto | null>(null)
  const [revokeTarget, setRevokeTarget] = useState<TenantInvitationDto | null>(null)
  const [resentLink, setResentLink] = useState<string | null>(null)

  const members = useQuery({ queryKey: ['team', 'members'], queryFn: getMembers })
  const invitations = useQuery({
    queryKey: ['team', 'invitations', { page, pageSize, status }],
    queryFn: () =>
      getInvitations({
        page,
        pageSize,
        status: status || undefined,
      }),
  })

  const removeMutation = useMutation({
    mutationFn: removeMember,
    onSuccess: () => {
      toast.success('Team member removed.')
      setRemoveTarget(null)
      void queryClient.invalidateQueries({ queryKey: ['team'] })
      void queryClient.invalidateQueries({ queryKey: ['usage'] })
      void queryClient.invalidateQueries({ queryKey: ['tenant', 'current'] })
      void queryClient.invalidateQueries({ queryKey: ['audit'] })
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const revokeMutation = useMutation({
    mutationFn: revokeInvitation,
    onSuccess: () => {
      toast.success('Invitation revoked.')
      setRevokeTarget(null)
      void queryClient.invalidateQueries({ queryKey: ['team'] })
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const resendMutation = useMutation({
    mutationFn: resendInvitation,
    onSuccess: (response) => {
      toast.success(`Invitation resent to ${response.invitation.email}`)
      void queryClient.invalidateQueries({ queryKey: ['team'] })
      if (response.token) {
        setResentLink(
          `${window.location.origin}/accept-invitation?token=${encodeURIComponent(response.token)}`,
        )
      }
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const canManageMember = (member: TenantMemberDto) => role === 'Owner' || member.role !== 'Owner'

  const memberColumns: DataTableColumn<TenantMemberDto>[] = [
    {
      id: 'member',
      header: 'Member',
      cell: (member) => (
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-foreground">{member.displayName || member.email}</p>
          <p className="mt-0.5 truncate text-xs text-muted-foreground md:hidden">{member.email}</p>
        </div>
      ),
    },
    {
      id: 'email',
      header: 'Email',
      className: 'hidden md:table-cell',
      cell: (member) => <span className="text-xs text-muted-foreground">{member.email}</span>,
    },
    {
      id: 'role',
      header: 'Role',
      cell: (member) => {
        const meta = tenantRoleMeta(member.role)
        return <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
      },
    },
    {
      id: 'status',
      header: 'Status',
      cell: () => <Badge variant="secondary">Active</Badge>,
    },
    {
      id: 'joined',
      header: 'Joined',
      className: 'hidden text-right lg:table-cell',
      cell: (member) => <span className="text-xs text-muted-foreground">{formatDate(member.joinedAt)}</span>,
    },
    {
      id: 'actions',
      header: '',
      className: 'text-right',
      cell: (member) =>
        canManageMember(member) ? (
          <div className="flex justify-end gap-1">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setRoleTarget(member)}
              className="h-7 px-2 text-xs"
            >
              Change role
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setRemoveTarget(member)}
              className="h-7 px-2 text-xs text-destructive hover:text-destructive"
            >
              <UserMinus className="size-3.5" aria-hidden />
              Remove
            </Button>
          </div>
        ) : null,
    },
  ]

  const invitationColumns: DataTableColumn<TenantInvitationDto>[] = [
    {
      id: 'email',
      header: 'Email',
      cell: (invitation) => <span className="text-sm text-foreground">{invitation.email}</span>,
    },
    {
      id: 'role',
      header: 'Role',
      cell: (invitation) => {
        const meta = tenantRoleMeta(invitation.role)
        return <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
      },
    },
    {
      id: 'status',
      header: 'Status',
      cell: (invitation) => {
        const meta = invitationStatusMeta(invitation.status)
        return <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
      },
    },
    {
      id: 'sent',
      header: 'Sent',
      className: 'hidden md:table-cell',
      cell: (invitation) => <span className="text-xs text-muted-foreground">{formatDate(invitation.lastSentAt)}</span>,
    },
    {
      id: 'expires',
      header: 'Expires',
      className: 'hidden lg:table-cell',
      cell: (invitation) => <span className="text-xs text-muted-foreground">{formatDate(invitation.expiresAt)}</span>,
    },
    {
      id: 'actions',
      header: '',
      className: 'text-right',
      cell: (invitation) => {
        const pending = invitation.status === 'Pending' || invitation.status === 'Expired'
        if (!pending) return null

        return (
          <div className="flex justify-end gap-1">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 px-2 text-xs"
              onClick={() => resendMutation.mutate(invitation.id)}
              disabled={resendMutation.isPending}
            >
              <RefreshCw className="size-3.5" aria-hidden />
              Resend
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 px-2 text-xs text-destructive hover:text-destructive"
              onClick={() => setRevokeTarget(invitation)}
            >
              <ShieldX className="size-3.5" aria-hidden />
              Revoke
            </Button>
          </div>
        )
      },
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold tracking-tight text-foreground">Team members</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Manage workspace access and pending invitations. Signed in as {roleLabel(role)}.
          </p>
        </div>
        <InviteMemberDialog />
      </div>

      <Tabs defaultValue="members">
        <TabsList>
          <TabsTrigger value="members">
            <Users className="size-4" aria-hidden />
            Members
          </TabsTrigger>
          <TabsTrigger value="invitations">
            <Mail className="size-4" aria-hidden />
            Invitations
          </TabsTrigger>
        </TabsList>

        <TabsContent value="members" className="mt-4">
          {members.isError ? (
            <ErrorState message={errorMessage(members.error)} onRetry={() => void members.refetch()} />
          ) : (
            <DataTable
              columns={memberColumns}
              rows={members.data ?? []}
              getRowKey={(member) => member.userId}
              caption="Workspace team members"
              isLoading={members.isLoading}
              empty={<EmptyState icon={<Users className="size-4" aria-hidden />} title="No team members" description="Invite a teammate to collaborate in this workspace." />}
            />
          )}
        </TabsContent>

        <TabsContent value="invitations" className="mt-4 space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Select
              value={status || 'all'}
              onValueChange={(value) => update({ status: value === 'all' ? undefined : value })}
            >
              <SelectTrigger className="h-9 w-[180px]" aria-label="Filter invitations by status">
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                <SelectItem value="Pending">Pending</SelectItem>
                <SelectItem value="Accepted">Accepted</SelectItem>
                <SelectItem value="Expired">Expired</SelectItem>
                <SelectItem value="Revoked">Revoked</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {invitations.isError ? (
            <ErrorState message={errorMessage(invitations.error)} onRetry={() => void invitations.refetch()} />
          ) : (
            <>
              <DataTable
                columns={invitationColumns}
                rows={invitations.data?.items ?? []}
                getRowKey={(invitation) => invitation.id}
                caption="Workspace invitations"
                isLoading={invitations.isLoading}
                empty={<EmptyState icon={<Mail className="size-4" aria-hidden />} title="No invitations" description="Invite a teammate to send the first invitation." />}
              />
              <ListPagination
                page={page}
                pageSize={pageSize}
                totalCount={invitations.data?.totalCount ?? 0}
                totalPages={invitations.data?.totalPages ?? 0}
                onPageChange={setPage}
                onPageSizeChange={(size) => update({ pageSize: size }, false)}
              />
            </>
          )}
        </TabsContent>
      </Tabs>

      {members.isLoading ? <Skeleton className="h-2 w-full" /> : null}

      {roleTarget ? (
        <ChangeRoleDialog member={roleTarget} open={Boolean(roleTarget)} onOpenChange={(open) => !open && setRoleTarget(null)} />
      ) : null}

      <ConfirmDialog
        open={Boolean(removeTarget)}
        onOpenChange={(open) => !open && setRemoveTarget(null)}
        title={removeTarget ? `Remove ${removeTarget.displayName || removeTarget.email}?` : 'Remove member'}
        description="They will immediately lose access to this workspace. Existing audit history is retained."
        confirmLabel="Remove member"
        destructive
        pending={removeMutation.isPending}
        onConfirm={() => removeTarget && removeMutation.mutate(removeTarget.userId)}
      />

      <ConfirmDialog
        open={Boolean(revokeTarget)}
        onOpenChange={(open) => !open && setRevokeTarget(null)}
        title={revokeTarget ? `Revoke invitation for ${revokeTarget.email}?` : 'Revoke invitation'}
        description="The invitation link will stop working immediately. You can send a new invitation later."
        confirmLabel="Revoke invitation"
        destructive
        pending={revokeMutation.isPending}
        onConfirm={() => revokeTarget && revokeMutation.mutate(revokeTarget.id)}
      />

      <Dialog open={Boolean(resentLink)} onOpenChange={(open) => !open && setResentLink(null)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Invitation resent</DialogTitle>
            <DialogDescription>
              The email sender is a logging stub in this build. Share the new invitation link directly.
            </DialogDescription>
          </DialogHeader>
          <div className="flex items-center gap-2 rounded-lg border border-warning/30 bg-warning/10 px-3 py-2 text-xs text-warning-foreground">
            <span className="min-w-0 flex-1 break-all">{resentLink}</span>
            {resentLink ? <CopyButton value={resentLink} label="invitation link" /> : null}
          </div>
          <DialogFooter>
            <Button type="button" onClick={() => setResentLink(null)}>
              Done
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {resendMutation.isPending ? (
        <p className="flex items-center gap-2 text-xs text-muted-foreground">
          <Loader2 className="size-3.5 animate-spin" aria-hidden />
          Sending invitation…
        </p>
      ) : null}
    </div>
  )
}
