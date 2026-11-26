// Date filters travel in the URL as `yyyy-mm-dd`, so they are parsed and
// formatted as a local calendar day with no timezone arithmetic. Using
// `new Date('2024-01-01')` would treat it as UTC midnight and shift the day
// for anyone west of Greenwich.
export function parseDateParam(value?: string): Date | undefined {
  if (!value) return undefined
  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day) return undefined
  const date = new Date(year, month - 1, day)
  return Number.isNaN(date.getTime()) ? undefined : date
}

export function toDateParam(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}
