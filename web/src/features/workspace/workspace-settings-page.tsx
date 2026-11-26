import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { Building2, Save } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { ErrorState } from '@/components/data/error-state'
import { getCurrentTenant, updateTenantSettings } from '@/features/workspace/api'
import { errorMessage } from '@/lib/api-client'
import { normalizeRole } from '@/lib/roles'
import { updateSessionTenant } from '@/lib/session'
import type { TenantSettingsDto } from '@/types/api'
import { qk } from '@/lib/query-keys'
import { useTenantKey } from '@/lib/use-tenant-key'
import { invalidateRelated } from '@/lib/invalidate'

const schema = z.object({
  name: z.string().trim().min(1, 'Enter a workspace name').max(160, 'Use 160 characters or fewer'),
  businessContactEmail: z
    .string()
    .trim()
    .max(320, 'Use 320 characters or fewer')
    .refine(
      (value) => value.length === 0 || z.string().email().safeParse(value).success,
      'Enter a valid email address or leave it blank',
    ),
  defaultCurrency: z
    .string()
    .trim()
    .length(3, 'Use a three-letter currency code')
    .regex(/^[A-Za-z]{3}$/, 'Use letters only'),
  timeZone: z.string().trim().min(1, 'Enter a time zone'),
})

type FormValues = z.infer<typeof schema>

function WorkspaceForm({ settings }: { settings: TenantSettingsDto }) {
  const queryClient = useQueryClient()
  const defaultValues = toFormValues(settings)
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues })

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      updateTenantSettings({
        name: values.name,
        businessContactEmail: values.businessContactEmail,
        defaultCurrency: values.defaultCurrency.toUpperCase(),
        timeZone: values.timeZone,
      }),
    onSuccess: (updated) => {
      updateSessionTenant({
        tenantId: updated.id,
        tenantName: updated.name,
        tenantSlug: updated.slug,
        role: normalizeRole(updated.role),
      })
      invalidateRelated(queryClient, 'tenant')
      form.reset(toFormValues(updated))
      toast.success('Workspace settings saved.')
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const onSubmit = form.handleSubmit((values) => mutation.mutate(values))

  return (
    <form className="space-y-5" onSubmit={onSubmit} noValidate>
      <div className="space-y-1.5">
        <Label htmlFor="workspaceName">Workspace name</Label>
        <Input id="workspaceName" autoComplete="organization" {...form.register('name')} />
        {form.formState.errors.name ? (
          <p className="text-xs text-destructive">{form.formState.errors.name.message}</p>
        ) : null}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="businessContactEmail">Business contact email</Label>
        <Input
          id="businessContactEmail"
          type="email"
          autoComplete="email"
          placeholder="billing@company.com"
          {...form.register('businessContactEmail')}
        />
        {form.formState.errors.businessContactEmail ? (
          <p className="text-xs text-destructive">{form.formState.errors.businessContactEmail.message}</p>
        ) : (
          <p className="text-xs text-muted-foreground">Used for workspace contact. Leave blank to clear it.</p>
        )}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="defaultCurrency">Default currency</Label>
          <Input
            id="defaultCurrency"
            maxLength={3}
            placeholder="USD"
            className="uppercase"
            {...form.register('defaultCurrency')}
          />
          {form.formState.errors.defaultCurrency ? (
            <p className="text-xs text-destructive">{form.formState.errors.defaultCurrency.message}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="timeZone">Time zone</Label>
          <Input id="timeZone" list="workspace-time-zones" placeholder="Europe/London" {...form.register('timeZone')} />
          <datalist id="workspace-time-zones">
            {timeZones.map((timeZone) => (
              <option key={timeZone} value={timeZone} />
            ))}
          </datalist>
          {form.formState.errors.timeZone ? (
            <p className="text-xs text-destructive">{form.formState.errors.timeZone.message}</p>
          ) : null}
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        <Button type="submit" loading={mutation.isPending}>
          <Save className="size-4" aria-hidden />
          Save changes
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={() => form.reset(defaultValues)}
          disabled={mutation.isPending}
        >
          Cancel
        </Button>
      </div>
    </form>
  )
}

export function WorkspaceSettingsPage() {
  const tenantId = useTenantKey()
  const settings = useQuery({ queryKey: qk.tenantCurrent(tenantId), queryFn: getCurrentTenant })

  if (settings.isError) {
    return <ErrorState message={errorMessage(settings.error)} onRetry={() => void settings.refetch()} />
  }

  if (settings.isLoading || !settings.data) {
    return (
      <div className="space-y-4" aria-busy="true" aria-label="Loading workspace settings">
        <Skeleton className="h-72 w-full rounded-xl" />
      </div>
    )
  }

  return (
    <Card className="border-border/80 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Building2 className="size-4 text-primary" aria-hidden />
          Workspace settings
        </CardTitle>
        <CardDescription>
          These values apply to everyone in the workspace. {timeZones.length > 0 ? 'Time zones are validated by the backend.' : ''}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <WorkspaceForm key={settings.data.updatedAt} settings={settings.data} />
      </CardContent>
    </Card>
  )
}

function toFormValues(settings: TenantSettingsDto): FormValues {
  return {
    name: settings.name,
    businessContactEmail: settings.businessContactEmail ?? '',
    defaultCurrency: settings.defaultCurrency,
    timeZone: settings.timeZone,
  }
}

const timeZones = getSupportedTimeZones()

function getSupportedTimeZones(): string[] {
  const intl = Intl as unknown as { supportedValuesOf?: (key: string) => string[] }
  try {
    return intl.supportedValuesOf?.('timeZone') ?? []
  } catch {
    return []
  }
}
