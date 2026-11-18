import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ArrowRight, CircleAlert, CircleCheck, Eye, EyeOff, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError, errorMessage } from '@/lib/api-client'
import { loginRequest } from '@/lib/auth-api'
import { clearSession, saveSession, sessionFromAuth } from '@/lib/session'
import { usePageTitle } from '@/lib/use-page-title'

const schema = z.object({
  email: z.string().trim().min(1, 'Enter your email address').email('Enter a valid email address'),
  password: z.string().min(1, 'Enter your password'),
})

type FormValues = z.infer<typeof schema>

export function SignInPage() {
  usePageTitle('Sign in')
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [params] = useSearchParams()
  const redirect = params.get('redirect')
  const redirectTo = redirect && redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/dashboard'
  const passwordChanged = params.get('passwordChanged') === '1'
  const passwordReset = params.get('passwordReset') === '1'
  const tenantSlug = tenantSlugFromHost(window.location.hostname)
  const [formError, setFormError] = useState<string | null>(null)
  const [showPassword, setShowPassword] = useState(false)

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)

    try {
      const response = await loginRequest(values.email, values.password)
      queryClient.clear()

      if (response.tenant) {
        saveSession(sessionFromAuth(response, {
          tenantId: response.tenant.tenantId,
          name: response.tenant.tenantName,
          slug: response.tenant.tenantSlug,
          role: response.tenant.role,
        }))
        navigate(redirectTo, { replace: true })
        return
      }

      if (response.user.isPlatformAdmin) {
        saveSession(sessionFromAuth(response, null))
        navigate('/platform', { replace: true })
        return
      }

      clearSession()
      setFormError('Sign in at your workspace subdomain.')
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.status === 401) {
          setFormError('Email or password is incorrect.')
          return
        }
        if (error.code === 'auth.tenant_required') {
          setFormError('Sign in at your workspace subdomain.')
          return
        }
        if (error.code === 'auth.tenant_membership_required') {
          setFormError('You are not a member of this workspace.')
          return
        }
      }
      setFormError(errorMessage(error))
    }
  })

  return (
    <div className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Welcome back</h1>
        <p className="text-sm leading-6 text-muted-foreground">
          Sign in to review payments, resolve exceptions and keep your reconciliation current.
        </p>
        {tenantSlug ? (
          <p className="text-xs text-muted-foreground">
            Workspace: <span className="font-medium text-foreground">{tenantSlug}</span>
          </p>
        ) : null}
      </div>

      {passwordChanged || passwordReset ? (
        <div className="flex items-start gap-2 rounded-md border border-success/30 bg-success/10 px-3 py-2.5 text-sm text-success">
          <CircleCheck className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>{passwordReset ? 'Password reset. Sign in with your new password.' : 'Password changed. Sign in again.'}</span>
        </div>
      ) : null}

      <form className="space-y-5" onSubmit={onSubmit} noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="email">Email</Label>
          <Input
            id="email"
            type="email"
            autoComplete="email"
            autoFocus
            placeholder="you@company.com"
            aria-invalid={Boolean(form.formState.errors.email)}
            {...form.register('email')}
          />
          {form.formState.errors.email ? (
            <p className="text-xs text-destructive">{form.formState.errors.email.message}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <div className="flex items-center justify-between gap-3">
            <Label htmlFor="password">Password</Label>
            <Link to="/forgot-password" className="text-xs font-medium text-primary hover:underline">
              Forgot password?
            </Link>
          </div>
          <div className="relative">
            <Input
              id="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              className="pr-10"
              aria-invalid={Boolean(form.formState.errors.password)}
              {...form.register('password')}
            />
            <button
              type="button"
              onClick={() => setShowPassword((visible) => !visible)}
              className="absolute inset-y-0 right-0 flex w-10 items-center justify-center rounded-r-md text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
              aria-label={showPassword ? 'Hide password' : 'Show password'}
            >
              {showPassword ? <EyeOff className="size-4" aria-hidden /> : <Eye className="size-4" aria-hidden />}
            </button>
          </div>
          {form.formState.errors.password ? (
            <p className="text-xs text-destructive">{form.formState.errors.password.message}</p>
          ) : null}
        </div>

        {formError ? (
          <div
            role="alert"
            aria-live="polite"
            className="flex items-start gap-2 rounded-md border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive"
          >
            <CircleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>{formError}</span>
          </div>
        ) : null}

        <Button className="w-full" size="lg" type="submit" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting ? (
            <>
              <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden />
              Signing in…
            </>
          ) : (
            <>
              Sign in
              <ArrowRight className="size-4" aria-hidden />
            </>
          )}
        </Button>
      </form>

      <p className="text-center text-sm text-muted-foreground">
        New to Ledgerline?{' '}
        <Link to="/sign-up" className="font-medium text-primary hover:underline">
          Create a workspace
        </Link>
      </p>
    </div>
  )
}

function tenantSlugFromHost(hostname: string): string | null {
  const host = hostname.toLowerCase()

  if (host === 'localhost' || host === '127.0.0.1' || host === '::1') {
    return null
  }

  const labels = host.split('.').filter(Boolean)
  if (labels.length < 2) {
    return null
  }

  const candidate = labels[0]
  if (candidate === 'www' || candidate === 'app' || candidate === 'api') {
    return null
  }

  return /^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/.test(candidate) && candidate.length >= 2 && candidate.length <= 40
    ? candidate
    : null
}
