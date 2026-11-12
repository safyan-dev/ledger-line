import { format, formatDistanceToNowStrict, parseISO } from 'date-fns'

export function formatMoney(amount: number, currency = 'USD'): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount)
}

export function formatAmount(amount: number): string {
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount)
}

export function formatDateTime(value: string): string {
  return format(parseISO(value), 'd MMM yyyy, HH:mm')
}

export function formatDate(value: string): string {
  return format(parseISO(value), 'd MMM yyyy')
}

export function formatShortDate(value: string): string {
  return format(parseISO(value), 'd MMM')
}

export function formatRelative(value: string): string {
  return `${formatDistanceToNowStrict(parseISO(value))} ago`
}

export function formatPercent(value: number): string {
  return `${value.toFixed(1).replace(/\.0$/, '')}%`
}

export function formatAge(hours: number): string {
  if (hours < 1) return 'just now'
  if (hours < 24) return `${hours}h`
  return `${Math.floor(hours / 24)}d`
}

export function formatSignedMoney(amount: number, currency = 'USD'): string {
  const formatted = formatMoney(Math.abs(amount), currency)
  if (amount === 0) return formatted
  return amount > 0 ? `+${formatted}` : `-${formatted}`
}

export function initials(name: string): string {
  return name
    .split(/[\s._-]+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}
