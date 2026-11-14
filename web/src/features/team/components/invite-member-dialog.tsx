import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, MailPlus } from 'lucide-react'
import { Controller, useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { CopyButton } from '@/components/data/copy-button'
import { inviteMember } from '@/features/team/api'
import { errorMessage } from '@/lib/api-client'

const schema = z.object({
  email: z.string().trim().min(1, 'Enter an email address').email('Enter a valid email address'),
  role: z.enum(['TenantAdmin', 'Finance']),
})

type FormValues = z.infer<typeof schema>

export function InviteMemberDialog() {
  const [open, setOpen] = useState(false)
  const [issuedToken, setIssuedToken] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', role: 'Finance' },
  })

  const mutation = useMutation({
    mutationFn: inviteMember,
    onSuccess: (response) => {
      toast.success(`Invitation sent to ${response.invitation.email}`)
      form.reset({ email: '', role: 'Finance' })
      void queryClient.invalidateQueries({ queryKey: ['team'] })
      void queryClient.invalidateQueries({ queryKey: ['usage'] })

      if (response.token) {
        setIssuedToken(response.token)
      } else {
        setOpen(false)
      }
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const onSubmit = form.handleSubmit((values) => mutation.mutate(values))

  const close = () => {
    setOpen(false)
    setIssuedToken(null)
    form.reset({ email: '', role: 'Finance' })
  }

  const invitationLink = issuedToken
    ? `${window.location.origin}/accept-invitation?token=${encodeURIComponent(issuedToken)}`
    : null

  return (
    <Dialog open={open} onOpenChange={(next) => (next ? setOpen(true) : close())}>
      <DialogTrigger asChild>
        <Button>
          <MailPlus className="size-4" aria-hidden />
          Invite member
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Invite a team member</DialogTitle>
          <DialogDescription>
            Send an email invitation for a TenantAdmin or Finance role. Owners must be assigned by an existing owner.
          </DialogDescription>
        </DialogHeader>

        {invitationLink ? (
          <div className="space-y-3 rounded-lg border border-warning/30 bg-warning/10 px-3 py-3 text-xs text-warning-foreground">
            <p className="font-medium">Development only</p>
            <p>The email sender is a logging stub in this build. Share this invitation link with the invitee:</p>
            <div className="flex items-center gap-2">
              <code className="min-w-0 flex-1 break-all rounded bg-background/70 px-2 py-1.5 text-[11px]">
                {invitationLink}
              </code>
              <CopyButton value={invitationLink} label="invitation link" />
            </div>
          </div>
        ) : (
          <form className="space-y-4" onSubmit={onSubmit} noValidate>
            <div className="space-y-1.5">
              <Label htmlFor="inviteEmail">Email</Label>
              <Input
                id="inviteEmail"
                type="email"
                placeholder="teammate@company.com"
                aria-invalid={Boolean(form.formState.errors.email)}
                {...form.register('email')}
              />
              {form.formState.errors.email ? (
                <p className="text-xs text-destructive">{form.formState.errors.email.message}</p>
              ) : null}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="inviteRole">Role</Label>
              <Controller
                control={form.control}
                name="role"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="inviteRole">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="TenantAdmin">Tenant admin</SelectItem>
                      <SelectItem value="Finance">Finance</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={close} disabled={mutation.isPending}>
                Cancel
              </Button>
              <Button type="submit" disabled={mutation.isPending}>
                {mutation.isPending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <MailPlus className="size-4" aria-hidden />}
                Send invitation
              </Button>
            </DialogFooter>
          </form>
        )}

        {invitationLink ? (
          <DialogFooter>
            <Button type="button" onClick={close}>
              Done
            </Button>
          </DialogFooter>
        ) : null}
      </DialogContent>
    </Dialog>
  )
}
