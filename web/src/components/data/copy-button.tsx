import { useEffect, useState } from 'react'
import { Check, Copy } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

export function CopyButton({
  value,
  label = 'value',
  className,
}: {
  value: string
  label?: string
  className?: string
}) {
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!copied) return
    const timer = window.setTimeout(() => setCopied(false), 1500)
    return () => window.clearTimeout(timer)
  }, [copied])

  async function copy() {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
    } catch {
      toast.error(`Could not copy ${label}.`)
    }
  }

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      onClick={(event) => {
        event.stopPropagation()
        void copy()
      }}
      className={cn('size-7 shrink-0 text-muted-foreground hover:text-foreground', className)}
      aria-label={`Copy ${label}`}
      title={copied ? 'Copied' : `Copy ${label}`}
    >
      {copied ? <Check className="size-3.5 text-success" aria-hidden /> : <Copy className="size-3.5" aria-hidden />}
    </Button>
  )
}

export function CopyableId({
  value,
  label = 'identifier',
  className,
}: {
  value: string
  label?: string
  className?: string
}) {
  return (
    <span className={cn('inline-flex min-w-0 max-w-full items-center gap-1', className)}>
      <span className="min-w-0 truncate font-mono text-xs" title={value}>
        {value}
      </span>
      <CopyButton value={value} label={label} />
    </span>
  )
}
