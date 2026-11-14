import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2, MailCheck } from 'lucide-react'
import { useForm } from 'react-hook-form'
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
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { confirmEmailChange, requestEmailChange } from '@/features/account/api'
import { errorMessage } from '@/lib/api-client'
import { updateSessionUser } from '@/lib/session'

const emailSchema = z.object({
  newEmail: z.string().trim().min(1, 'Enter the new email address').email('Enter a valid email address'),
})

type EmailFormValues = z.infer<typeof emailSchema>

export function ChangeEmailDialog({
  open,
  onOpenChange,
  currentEmail,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentEmail: string
}) {
  const queryClient = useQueryClient()
  const [step, setStep] = useState<'email' | 'token'>('email')
  const [token, setToken] = useState('')
  const [devToken, setDevToken] = useState<string | null>(null)

  const form = useForm<EmailFormValues>({
    resolver: zodResolver(emailSchema),
    defaultValues: { newEmail: '' },
  })

  const close = () => {
    onOpenChange(false)
    setStep('email')
    setToken('')
    setDevToken(null)
    form.reset({ newEmail: '' })
  }

  const requestMutation = useMutation({
    mutationFn: requestEmailChange,
    onSuccess: (response) => {
      setDevToken(response.verificationToken)
      setToken(response.verificationToken ?? '')
      setStep('token')
      toast.success('Verification token sent to the new email address.')
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const confirmMutation = useMutation({
    mutationFn: () => confirmEmailChange(form.getValues('newEmail'), token),
    onSuccess: (user) => {
      updateSessionUser(user)
      void queryClient.invalidateQueries({ queryKey: ['account', 'profile'] })
      toast.success('Email address updated.')
      close()
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const onRequest = form.handleSubmit((values) => {
    if (values.newEmail.trim().toLowerCase() === currentEmail.toLowerCase()) {
      form.setError('newEmail', { message: 'The new email must be different from the current email.' })
      return
    }
    requestMutation.mutate(values.newEmail)
  })

  return (
    <Dialog open={open} onOpenChange={(next) => (next ? onOpenChange(true) : close())}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Change email address</DialogTitle>
          <DialogDescription>
            The new address must be verified before it becomes the account email.
          </DialogDescription>
        </DialogHeader>

        {step === 'email' ? (
          <form className="space-y-4" onSubmit={onRequest} noValidate>
            <div className="space-y-1.5">
              <Label htmlFor="newEmail">New email</Label>
              <Input
                id="newEmail"
                type="email"
                autoComplete="email"
                placeholder="you@company.com"
                aria-invalid={Boolean(form.formState.errors.newEmail)}
                {...form.register('newEmail')}
              />
              {form.formState.errors.newEmail ? (
                <p className="text-xs text-destructive">{form.formState.errors.newEmail.message}</p>
              ) : null}
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={close} disabled={requestMutation.isPending}>
                Cancel
              </Button>
              <Button type="submit" disabled={requestMutation.isPending}>
                {requestMutation.isPending ? (
                  <Loader2 className="size-4 animate-spin" aria-hidden />
                ) : (
                  <MailCheck className="size-4" aria-hidden />
                )}
                Send verification
              </Button>
            </DialogFooter>
          </form>
        ) : (
          <div className="space-y-4">
            {devToken ? (
              <div className="rounded-lg border border-warning/30 bg-warning/10 px-3 py-2 text-xs text-warning-foreground">
                <p className="font-medium">Development only</p>
                <p className="mt-1 break-all">Use this token: {devToken}</p>
              </div>
            ) : null}
            <div className="space-y-1.5">
              <Label htmlFor="emailToken">Verification token</Label>
              <Input
                id="emailToken"
                value={token}
                onChange={(event) => setToken(event.target.value)}
                placeholder="Paste the token from the verification email"
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={close} disabled={confirmMutation.isPending}>
                Cancel
              </Button>
              <Button
                type="button"
                onClick={() => confirmMutation.mutate()}
                disabled={!token.trim() || confirmMutation.isPending}
              >
                {confirmMutation.isPending ? (
                  <Loader2 className="size-4 animate-spin" aria-hidden />
                ) : (
                  <MailCheck className="size-4" aria-hidden />
                )}
                Confirm email
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
