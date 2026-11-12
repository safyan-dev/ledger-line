import { useState } from 'react'
import { Search, X } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

export function SearchInput({
  defaultValue = '',
  onCommit,
  placeholder = 'Search…',
  className,
}: {
  defaultValue?: string
  onCommit?: (value: string) => void
  placeholder?: string
  className?: string
}) {
  const [value, setValue] = useState(defaultValue)

  return (
    <form
      className={cn('relative w-full sm:w-72', className)}
      onSubmit={(event) => {
        event.preventDefault()
        onCommit?.(value.trim())
      }}
      role="search"
    >
      <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
      <Input
        value={value}
        onChange={(event) => setValue(event.target.value)}
        placeholder={placeholder}
        className="h-9 pl-8 pr-8"
        aria-label={placeholder}
      />
      {value ? (
        <button
          type="button"
          onClick={() => {
            setValue('')
            onCommit?.('')
          }}
          className="absolute right-1.5 top-1/2 flex size-6 -translate-y-1/2 items-center justify-center rounded text-muted-foreground hover:bg-muted hover:text-foreground"
          aria-label="Clear search"
        >
          <X className="size-3.5" aria-hidden />
        </button>
      ) : null}
    </form>
  )
}
