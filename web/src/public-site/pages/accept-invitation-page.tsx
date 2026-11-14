import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CircleAlert, Loader2, UserPlus } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { acceptInvitation } from '@/lib/invitation-api'
import { ApiError, errorMessage } from '@/lib/api-client'
import { normalizeRole } from '@/lib/roles'
import { saveSession, updateSessionTenant, useSession, type AuthSession } from '@/lib/session'
import { usePageTitle } from '@/lib/use-page-title'

const schema = z
  .object({
    displayName: z.string().trim().min(1, 'Enter your name').max(160, 'Use 160 characters or fewer'),
    password: z
      .string()
      .min(10, 'Use at least 10 characters')
      .regex(/[A-Z]/, 'Include an uppercase letter')
      .regex(/[a-z]/, 'Include a lowercase letter')
      .regex(/[0-9]/, 'Include a number')
      .regex(/[^A-Za-z0-9]/, 'Include a symbol'),
    confirmPassword: z.string().min(1, 'Confirm your password'),
  })
  .refine((values) => values.password === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Passwords do not match',
  })

type FormValues = z.infer<typeof schema>

export function AcceptInvitationPage() {
  usePageTitle('Accept invitation')
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const session = useSession()
  const token = params.get('token')?.trim() ?? ''
  const [formError, setFormError] = useState<string | null>(null)
  const [needsSignIn, setNeedsSignIn] = useState(false)

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { displayName: '', password: '', confirmPassword: '' },
  })

  const acceptWithAccount = useMutation({
    mutationFn: () => acceptInvitation({ token }),
    onSuccess: (response) => {
      queryClient.clear()
      updateSessionTenant({
        tenantId: response.tenantId,
        tenantName: response.tenantName,
        tenantSlug: response.tenantSlug,
        role: normalizeRole(response.role),
      })
      navigate('/dashboard', { replace: true })
    },
    onError: (error) => setFormError(errorMessage(error)),
  })

  const acceptWithNewAccount = useMutation({
    mutationFn: (values: FormValues) =>
      acceptInvitation({ token, displayName: values.displayName, password: values.password }),
    onSuccess: (response) => {
      queryClient.clear()
      if (response.session) {
        const next: AuthSession = {
          accessToken: response.session.accessToken,
          refreshToken: response.session.refreshToken,
          tenantId: response.tenantId,
          tenantName: response.tenantName,
          tenantSlug: response.tenantSlug,
          role: normalizeRole(response.role),
          user: response.session.user,
        }
        saveSession(next)
      }
      navigate('/dashboard', { replace: true })
    },
    onError: (error) => {
      if (error instanceof ApiError && error.code === 'invitation.login_required') {
        setNeedsSignIn(true)
        return
      }
      setFormError(errorMessage(error))
    },
  })

  const onSubmit = form.handleSubmit((values) => {
    setFormError(null)
    setNeedsSignIn(false)
    acceptWithNewAccount.mutate(values)
  })

  if (!token) {
    return (
      <div className="space-y-4 text-center">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Invitation link is incomplete</h1>
        <p className="text-sm text-muted-foreground">Ask your workspace administrator for a new invitation link.</p>
        <Button asChild>
          <Link to="/sign-in">Back to sign in</Link>
        </Button>
      </div>
    )
  }

  const signInHref = `/sign-in?redirect=${encodeURIComponent(`/accept-invitation?token=${token}`)}`

  return (
    <div className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Join the workspace</h1>
        <p className="text-sm leading-6 text-muted-foreground">
          Accept the invitation to add this workspace to your account.
        </p>
      </div>

      {session ? (
        <div className="space-y-4">
          <div className="rounded-lg border bg-card px-3.5 py-3 text-sm">
            <p className="font-medium text-foreground">{session.user.displayName || session.user.email}</p>
            <p className="text-xs text-muted-foreground">{session.user.email}</p>
          </div>
          {formError ? <ErrorBlock message={formError} /> : null}
          <Button
            className="w-full"
            size="lg"
            onClick={() => {
              setFormError(null)
              acceptWithAccount.mutate()
            }}
            disabled={acceptWithAccount.isPending}
          >
            {acceptWithAccount.isPending ? (
              <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden />
            ) : (
              <UserPlus className="size-4" aria-hidden />
            )}
            Accept with this account
          </Button>
        </div>
      ) : (
        <form className="space-y-5" onSubmit={onSubmit} noValidate>
          <div className="space-y-1.5">
            <Label htmlFor="displayName">Your name</Label>
            <Input id="displayName" autoComplete="name" placeholder="Avery Chen" {...form.register('displayName')} />
            {form.formState.errors.displayName ? (
              <p className="text-xs text-destructive">{form.formState.errors.displayName.message}</p>
            ) : null}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="password">Password</Label>
            <Input id="password" type="password" autoComplete="new-password" {...form.register('password')} />
            {form.formState.errors.password ? (
              <p className="text-xs text-destructive">{form.formState.errors.password.message}</p>
            ) : null}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="confirmPassword">Confirm password</Label>
            <Input id="confirmPassword" type="password" autoComplete="new-password" {...form.register('confirmPassword')} />
            {form.formState.errors.confirmPassword ? (
              <p className="text-xs text-destructive">{form.formState.errors.confirmPassword.message}</p>
            ) : null}
          </div>

          {needsSignIn ? (
            <div className="space-y-2 rounded-md border border-warning/30 bg-warning/10 px-3 py-2.5 text-xs text-warning-foreground">
              <p>An account already exists for this email. Sign in, then accept the invitation.</p>
              <Link to={signInHref} className="font-medium text-primary hover:underline">
                Sign in to accept
              </Link>
            </div>
          ) : null}

          {formError ? <ErrorBlock message={formError} /> : null}

          <Button className="w-full" size="lg" type="submit" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting ? (
              <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden />
            ) : (
              <UserPlus className="size-4" aria-hidden />
            )}
            Create account and join
          </Button>

          <p className="text-center text-sm text-muted-foreground">
            Already have an account?{' '}
            <Link to={signInHref} className="font-medium text-primary hover:underline">
              Sign in
            </Link>
          </p>
        </form>
      )}
    </div>
  )
}

function ErrorBlock({ message }: { message: string }) {
  return (
    <div
      role="alert"
      aria-live="polite"
      className="flex items-start gap-2 rounded-md border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive"
    >
      <CircleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
      <span>{message}</span>
    </div>
  )
}
