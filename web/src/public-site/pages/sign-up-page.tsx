import { useState, type ChangeEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ArrowRight, Circle, CircleAlert, CircleCheck, Eye, EyeOff, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError, errorMessage } from '@/lib/api-client'
import { registerTenantRequest } from '@/lib/auth-api'
import { normalizeRole } from '@/lib/roles'
import { getSelectedPlan, saveSession, type SelectedPlan } from '@/lib/session'
import { usePageTitle } from '@/lib/use-page-title'
import { cn } from '@/lib/utils'

const schema = z.object({
  companyName: z.string().trim().min(2, 'Enter your company name').max(120),
  tenantSlug: z
    .string()
    .trim()
    .toLowerCase()
    .min(2, 'Use at least 2 characters')
    .max(40, 'Use at most 40 characters')
    .regex(/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/, 'Use lowercase letters, numbers and hyphens'),
  ownerName: z.string().trim().min(2, 'Enter your name').max(160),
  email: z.string().trim().min(1, 'Enter your email address').email('Enter a valid email address'),
  password: z
    .string()
    .min(10, 'Use at least 10 characters')
    .regex(/[A-Z]/, 'Include an uppercase letter')
    .regex(/[a-z]/, 'Include a lowercase letter')
    .regex(/[0-9]/, 'Include a number')
    .regex(/[^A-Za-z0-9]/, 'Include a symbol'),
})

type FormValues = z.infer<typeof schema>

function slugify(value: string): string {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
}

function titleCase(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

export function SignUpPage() {
  usePageTitle('Get started')
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [params] = useSearchParams()
  const [formError, setFormError] = useState<string | null>(null)
  const [slugTouched, setSlugTouched] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [selectedPlan] = useState<SelectedPlan | null>(() => {
    const code = params.get('plan')
    const stored = getSelectedPlan()

    if (!code) return stored
    return stored?.code === code ? stored : { code, name: titleCase(code) }
  })

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      companyName: '',
      tenantSlug: '',
      ownerName: '',
      email: '',
      password: '',
    },
  })

  const password = useWatch({ control: form.control, name: 'password' }) ?? ''
  const requirements = [
    { label: '10+ characters', met: password.length >= 10 },
    { label: 'Uppercase letter', met: /[A-Z]/.test(password) },
    { label: 'Lowercase letter', met: /[a-z]/.test(password) },
    { label: 'Number', met: /[0-9]/.test(password) },
    { label: 'Symbol', met: /[^A-Za-z0-9]/.test(password) },
  ]

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)

    try {
      const response = await registerTenantRequest({
        ...values,
        planCode: selectedPlan?.code,
      })

      queryClient.clear()
      saveSession({
        accessToken: response.session.accessToken,
        refreshToken: response.session.refreshToken,
        tenantId: response.tenant.id,
        tenantName: response.tenant.name,
        tenantSlug: response.tenant.slug,
        role: normalizeRole(response.tenant.role),
        user: response.session.user,
      })

      navigate('/dashboard', { replace: true })
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setFormError(error.message)
        return
      }
      setFormError(errorMessage(error))
    }
  })

  return (
    <div className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Create your workspace</h1>
        <p className="text-sm leading-6 text-muted-foreground">
          Set up your company and start reconciling payments in minutes.
        </p>
      </div>

      <div className="flex items-center justify-between gap-3 rounded-lg border bg-card px-3.5 py-3">
        <div>
          <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Plan</p>
          <p className="mt-0.5 text-sm font-semibold text-foreground">{selectedPlan?.name ?? 'Free'}</p>
        </div>
        <p className="max-w-[180px] text-right text-[11px] leading-4 text-muted-foreground">
          You can change plans later in Settings.
        </p>
      </div>

      <form className="space-y-5" onSubmit={onSubmit} noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="companyName">Company name</Label>
          <Input
            id="companyName"
            autoComplete="organization"
            autoFocus
            placeholder="Northwind Finance"
            aria-invalid={Boolean(form.formState.errors.companyName)}
            {...form.register('companyName', {
              onChange: (event: ChangeEvent<HTMLInputElement>) => {
                if (!slugTouched) {
                  form.setValue('tenantSlug', slugify(event.target.value), { shouldValidate: false })
                }
              },
            })}
          />
          {form.formState.errors.companyName ? (
            <p className="text-xs text-destructive">{form.formState.errors.companyName.message}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="tenantSlug">Workspace slug</Label>
          <Input
            id="tenantSlug"
            autoComplete="off"
            placeholder="northwind"
            aria-invalid={Boolean(form.formState.errors.tenantSlug)}
            {...form.register('tenantSlug', {
              onChange: () => setSlugTouched(true),
            })}
          />
          {form.formState.errors.tenantSlug ? (
            <p className="text-xs text-destructive">{form.formState.errors.tenantSlug.message}</p>
          ) : (
            <p className="text-xs text-muted-foreground">Lowercase letters, numbers and hyphens.</p>
          )}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="ownerName">Your name</Label>
          <Input
            id="ownerName"
            autoComplete="name"
            placeholder="Avery Chen"
            aria-invalid={Boolean(form.formState.errors.ownerName)}
            {...form.register('ownerName')}
          />
          {form.formState.errors.ownerName ? (
            <p className="text-xs text-destructive">{form.formState.errors.ownerName.message}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="email">Work email</Label>
          <Input
            id="email"
            type="email"
            autoComplete="email"
            placeholder="you@company.com"
            aria-invalid={Boolean(form.formState.errors.email)}
            {...form.register('email')}
          />
          {form.formState.errors.email ? (
            <p className="text-xs text-destructive">{form.formState.errors.email.message}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="password">Password</Label>
          <div className="relative">
            <Input
              id="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              className="pr-10"
              aria-invalid={Boolean(form.formState.errors.password)}
              aria-describedby="password-requirements"
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

          <ul
            id="password-requirements"
            aria-label="Password requirements"
            className="grid grid-cols-2 gap-x-3 gap-y-1.5 pt-1"
          >
            {requirements.map((requirement) => (
              <li
                key={requirement.label}
                className={cn(
                  'flex items-center gap-1.5 text-xs',
                  requirement.met ? 'text-success' : 'text-muted-foreground',
                )}
              >
                {requirement.met ? (
                  <CircleCheck className="size-3.5 shrink-0" aria-hidden />
                ) : (
                  <Circle className="size-3.5 shrink-0" aria-hidden />
                )}
                {requirement.label}
              </li>
            ))}
          </ul>
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
              Creating workspace…
            </>
          ) : (
            <>
              Create workspace
              <ArrowRight className="size-4" aria-hidden />
            </>
          )}
        </Button>
      </form>

      <p className="text-center text-sm text-muted-foreground">
        Already have an account?{' '}
        <Link to="/sign-in" className="font-medium text-primary hover:underline">
          Sign in
        </Link>
      </p>
    </div>
  )
}
