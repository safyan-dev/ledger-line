import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ArrowLeft, CircleAlert, CircleCheck, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { confirmEmail } from '@/lib/auth-api'
import { errorMessage } from '@/lib/api-client'
import { usePageTitle } from '@/lib/use-page-title'

export function ConfirmEmailPage() {
  usePageTitle('Confirm your email')
  const [params] = useSearchParams()
  const email = params.get('email')
  const token = params.get('token')
  const [status, setStatus] = useState<'pending' | 'success' | 'error'>(() =>
    email && token ? 'pending' : 'error',
  )
  const [message, setMessage] = useState(() =>
    email && token
      ? 'Confirming your email address…'
      : 'This confirmation link is incomplete. Request a new confirmation email and try again.',
  )

  useEffect(() => {
    if (!email || !token) {
      return
    }

    let active = true
    confirmEmail(email, token)
      .then(() => {
        if (!active) return
        setStatus('success')
        setMessage('Your email address is confirmed. You can now sign in.')
      })
      .catch((error) => {
        if (!active) return
        setStatus('error')
        setMessage(errorMessage(error))
      })

    return () => {
      active = false
    }
  }, [email, token])

  return (
    <div className="space-y-8">
      <Link
        to="/sign-in"
        className="inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="size-3.5" aria-hidden />
        Back to sign in
      </Link>

      <div className="space-y-4">
        <div
          className={
            status === 'success'
              ? 'flex size-11 items-center justify-center rounded-full bg-success/15 text-success'
              : status === 'error'
                ? 'flex size-11 items-center justify-center rounded-full bg-destructive/10 text-destructive'
                : 'flex size-11 items-center justify-center rounded-full bg-muted text-muted-foreground'
          }
        >
          {status === 'success' ? (
            <CircleCheck className="size-5" aria-hidden />
          ) : status === 'error' ? (
            <CircleAlert className="size-5" aria-hidden />
          ) : (
            <Loader2 className="size-5 animate-spin motion-reduce:animate-none" aria-hidden />
          )}
        </div>

        <div className="space-y-2">
          <h1 className="text-2xl font-semibold tracking-tight text-foreground">Confirm your email</h1>
          <p className="text-sm leading-6 text-muted-foreground">{message}</p>
        </div>

        {status === 'success' ? (
          <Button asChild className="w-full" size="lg">
            <Link to="/sign-in">Sign in</Link>
          </Button>
        ) : null}
      </div>
    </div>
  )
}
