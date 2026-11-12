import { useState } from 'react'
import { Check, Copy } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

export function JsonViewer({ value, className }: { value: string; className?: string }) {
  const [copied, setCopied] = useState(false)

  let formatted = value
  try {
    formatted = JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    formatted = value
  }

  const copy = async () => {
    await navigator.clipboard.writeText(formatted)
    setCopied(true)
    window.setTimeout(() => setCopied(false), 1500)
  }

  return (
    <div className={cn('relative rounded-lg border bg-slate-950', className)}>
      <Button
        variant="ghost"
        size="sm"
        onClick={copy}
        className="absolute right-2 top-2 z-10 h-7 gap-1.5 text-slate-300 hover:bg-slate-800 hover:text-slate-100"
      >
        {copied ? <Check className="size-3.5" aria-hidden /> : <Copy className="size-3.5" aria-hidden />}
        {copied ? 'Copied' : 'Copy'}
      </Button>
      <pre className="max-h-[380px] overflow-auto p-4 pr-24 text-xs leading-relaxed text-slate-100">
        <code>{formatted}</code>
      </pre>
    </div>
  )
}
