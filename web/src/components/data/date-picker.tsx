import { useState } from 'react'
import { CalendarIcon, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { parseDateParam, toDateParam } from '@/lib/date-param'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'

export function DatePicker({
  value,
  onChange,
  placeholder = 'Pick a date',
  ariaLabel,
  fromDate,
  toDate,
  clearable = true,
  className,
}: {
  value?: string
  onChange: (value: string | undefined) => void
  placeholder?: string
  ariaLabel?: string
  fromDate?: Date
  toDate?: Date
  clearable?: boolean
  className?: string
}) {
  const [open, setOpen] = useState(false)
  const selected = parseDateParam(value)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          aria-label={ariaLabel}
          className={cn(
            'h-9 w-[145px] justify-start gap-2 px-2.5 font-normal',
            !selected && 'text-muted-foreground',
            className,
          )}
        >
          <CalendarIcon className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
          <span className="truncate">{selected ? formatDate(toDateParam(selected)) : placeholder}</span>
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto p-0" align="start">
        <Calendar
          mode="single"
          selected={selected}
          defaultMonth={selected}
          onSelect={(date) => {
            onChange(date ? toDateParam(date) : undefined)
            setOpen(false)
          }}
          disabled={[
            ...(fromDate ? [{ before: fromDate }] : []),
            ...(toDate ? [{ after: toDate }] : []),
          ]}
          autoFocus
        />
        {clearable && selected ? (
          <div className="border-t p-2">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="w-full justify-start gap-2 text-muted-foreground"
              onClick={() => {
                onChange(undefined)
                setOpen(false)
              }}
            >
              <X className="size-3.5" aria-hidden />
              Clear date
            </Button>
          </div>
        ) : null}
      </PopoverContent>
    </Popover>
  )
}
