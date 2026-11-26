import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { BadgeCheck, CircleAlert, Mail, Save, ShieldCheck } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { z } from 'zod'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { ErrorState } from '@/components/data/error-state'
import { ChangeEmailDialog } from '@/features/account/components/change-email-dialog'
import { ChangePasswordForm } from '@/features/account/components/change-password-form'
import { getProfile, updateProfile } from '@/features/account/api'
import { errorMessage } from '@/lib/api-client'
import { usePermissions } from '@/lib/permissions'
import { roleLabel } from '@/lib/roles'
import { updateSessionUser } from '@/lib/session'
import type { AuthUser } from '@/lib/auth-api'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'

const profileSchema = z.object({
  displayName: z.string().trim().min(1, 'Enter your name').max(160, 'Use 160 characters or fewer'),
})

type ProfileFormValues = z.infer<typeof profileSchema>

function ProfileForm({ profile }: { profile: AuthUser }) {
  const tenantId = useTenantKey()
  const queryClient = useQueryClient()
  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { displayName: profile.displayName },
  })

  const mutation = useMutation({
    mutationFn: updateProfile,
    onSuccess: (user) => {
      updateSessionUser(user)
      void queryClient.invalidateQueries({ queryKey: qk.accountProfile(tenantId) })
      toast.success('Profile updated.')
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const onSubmit = form.handleSubmit((values) => mutation.mutate(values))

  return (
    <form className="space-y-4" onSubmit={onSubmit} noValidate>
      <div className="space-y-1.5">
        <Label htmlFor="displayName">Display name</Label>
        <Input id="displayName" autoComplete="name" {...form.register('displayName')} />
        {form.formState.errors.displayName ? (
          <p className="text-xs text-destructive">{form.formState.errors.displayName.message}</p>
        ) : null}
      </div>
      <Button type="submit" loading={mutation.isPending}>
        <Save className="size-4" aria-hidden />
        Save profile
      </Button>
    </form>
  )
}

export function AccountPage() {
  const { role } = usePermissions()
  const [emailOpen, setEmailOpen] = useState(false)
  const profile = useQuery({ queryKey: qk.accountProfile(useTenantKey()), queryFn: getProfile })

  if (profile.isError) {
    return <ErrorState message={errorMessage(profile.error)} onRetry={() => void profile.refetch()} />
  }

  if (profile.isLoading || !profile.data) {
    return (
      <div className="space-y-4" aria-busy="true" aria-label="Loading account">
        <Skeleton className="h-44 w-full rounded-xl" />
        <Skeleton className="h-36 w-full rounded-xl" />
        <Skeleton className="h-56 w-full rounded-xl" />
      </div>
    )
  }

  const user = profile.data

  return (
    <div className="space-y-6">
      <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <BadgeCheck className="size-4 text-primary" aria-hidden />
            Profile
          </CardTitle>
          <CardDescription>Your name appears in audit history and team activity.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            <span>{user.email}</span>
            <Badge variant="outline">{roleLabel(role)}</Badge>
          </div>
          <ProfileForm profile={user} />
        </CardContent>
      </Card>

      <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Mail className="size-4 text-primary" aria-hidden />
            Email address
          </CardTitle>
          <CardDescription>Email changes require verification of the new address.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-sm font-medium text-foreground">{user.email}</span>
            {user.emailConfirmed ? (
              <Badge variant="secondary" className="gap-1">
                <BadgeCheck className="size-3" aria-hidden />
                Verified
              </Badge>
            ) : (
              <Badge variant="outline" className="gap-1 text-warning-foreground">
                <CircleAlert className="size-3" aria-hidden />
                Unverified
              </Badge>
            )}
          </div>
          <Button variant="outline" onClick={() => setEmailOpen(true)}>
            Change email
          </Button>
          <ChangeEmailDialog open={emailOpen} onOpenChange={setEmailOpen} currentEmail={user.email} />
        </CardContent>
      </Card>

      <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ShieldCheck className="size-4 text-primary" aria-hidden />
            Password & security
          </CardTitle>
          <CardDescription>
            Change your password, or{' '}
            <Link to="/forgot-password" className="font-medium text-primary hover:underline">
              send yourself a reset link
            </Link>
            .
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ChangePasswordForm />
        </CardContent>
      </Card>
    </div>
  )
}
