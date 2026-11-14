import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeft, Loader2, ShieldCheck } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { resetPassword } from '@/lib/auth-api'
import { errorMessage } from '@/lib/api-client'
import { usePageTitle } from '@/lib/use-page-title'

const schema = z
  .object({
    email: z.string().trim().min(1, 'Enter your email address').email('Enter a valid email address'),
    token: z.string().trim().min(1, 'Enter the reset token'),
    newPassword: z
      .string()
      .min(10, 'Use at least 10 characters')
      .regex(/[A-Z]/, 'Include an uppercase letter')
      .regex(/[a-z]/, 'Include a lowercase letter')
      .regex(/[0-9]/, 'Include a number')
      .regex(/[^A-Za-z0-9]/, 'Include a symbol'),
    confirmPassword: z.string().min(1, 'Confirm the new password'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Passwords do not match',
  })

type FormValues = z.infer<typeof schema>

export function ResetPasswordPage() {
  usePageTitle('Choose a new password')
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      email: params.get('email') ?? '',
      token: params.get('token') ?? '',
      newPassword: '',
      confirmPassword: '',
    },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)

    try {
      await resetPassword(values.email, values.token, values.newPassword)
      navigate('/sign-in?passwordReset=1', { replace: true })
    } catch (error) {
      setFormError(errorMessage(error))
    }
  })

  return (
    <div className="space-y-8">
      <Link to="/sign-in" className="inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-3.5" aria-hidden />
        Back to sign in
      </Link>

      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Choose a new password</h1>
        <p className="text-sm leading-6 text-muted-foreground">
          Reset tokens are single-use and expire after one hour.
        </p>
      </div>

      <form className="space-y-5" onSubmit={onSubmit} noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="email">Email</Label>
          <Input id="email" type="email" autoComplete="email" {...form.register('email')} />
          {form.formState.errors.email ? (
            <p className="text-xs text-destructive">{form.formState.errors.email.message}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="token">Reset token</Label>
          <Input id="token" autoComplete="one-time-code" {...form.register('token')} />
          {form.formState.errors.token ? (
            <p className="text-xs text-destructive">{form.formState.errors.token.message}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="newPassword">New password</Label>
          <Input id="newPassword" type="password" autoComplete="new-password" {...form.register('newPassword')} />
          {form.formState.errors.newPassword ? (
            <p className="text-xs text-destructive">{form.formState.errors.newPassword.message}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="confirmPassword">Confirm new password</Label>
          <Input id="confirmPassword" type="password" autoComplete="new-password" {...form.register('confirmPassword')} />
          {form.formState.errors.confirmPassword ? (
            <p className="text-xs text-destructive">{form.formState.errors.confirmPassword.message}</p>
          ) : null}
        </div>

        {formError ? (
          <div role="alert" className="rounded-md border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">
            {formError}
          </div>
        ) : null}

        <Button className="w-full" size="lg" type="submit" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting ? (
            <Loader2 className="size-4 animate-spin motion-reduce:animate-none" aria-hidden />
          ) : (
            <ShieldCheck className="size-4" aria-hidden />
          )}
          Reset password
        </Button>
      </form>
    </div>
  )
}
