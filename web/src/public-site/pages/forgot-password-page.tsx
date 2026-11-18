import { useState } from 'react'
import { Link } from 'react-router-dom'
import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeft, CircleCheck, Loader2, Mail } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { forgotPassword } from '@/lib/auth-api'
import { errorMessage } from '@/lib/api-client'
import { usePageTitle } from '@/lib/use-page-title'

const schema = z.object({
  email: z.string().trim().min(1, 'Enter your email address').email('Enter a valid email address'),
})

type FormValues = z.infer<typeof schema>

export function ForgotPasswordPage() {
  usePageTitle('Forgot password')
  const [sent, setSent] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '' },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)

    try {
      await forgotPassword(values.email)
      setSent(true)
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
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Reset your password</h1>
        <p className="text-sm leading-6 text-muted-foreground">
          Enter your account email and we will send a single-use reset token.
        </p>
      </div>

      {sent ? (
        <div className="space-y-5">
          <div className="flex items-start gap-2 rounded-md border border-success/30 bg-success/10 px-3 py-2.5 text-sm text-success">
            <CircleCheck className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>If an account exists for that email, a reset token has been sent.</span>
          </div>

          <Button asChild variant="outline" className="w-full">
            <Link to="/reset-password">I have a reset token</Link>
          </Button>
        </div>
      ) : (
        <form className="space-y-5" onSubmit={onSubmit} noValidate>
          <div className="space-y-1.5">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              autoFocus
              placeholder="you@company.com"
              {...form.register('email')}
            />
            {form.formState.errors.email ? (
              <p className="text-xs text-destructive">{form.formState.errors.email.message}</p>
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
              <Mail className="size-4" aria-hidden />
            )}
            Send reset token
          </Button>
        </form>
      )}
    </div>
  )
}
