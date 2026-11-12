export type Tone = 'success' | 'info' | 'warning' | 'danger' | 'neutral' | 'outline'

export type StatusMeta = {
  label: string
  tone: Tone
}

const orderStatuses: Record<string, StatusMeta> = {
  Pending: { label: 'Pending', tone: 'neutral' },
  PartiallyPaid: { label: 'Partially paid', tone: 'info' },
  Paid: { label: 'Paid', tone: 'success' },
  Overpaid: { label: 'Overpaid', tone: 'warning' },
  Cancelled: { label: 'Cancelled', tone: 'danger' },
}

const paymentStatuses: Record<string, StatusMeta> = {
  Received: { label: 'Received', tone: 'neutral' },
  Matched: { label: 'Matched', tone: 'success' },
  Unmatched: { label: 'Unmatched', tone: 'warning' },
  Failed: { label: 'Failed', tone: 'danger' },
  PartiallyRefunded: { label: 'Partially refunded', tone: 'info' },
  Refunded: { label: 'Refunded', tone: 'outline' },
}

const webhookStatuses: Record<string, StatusMeta> = {
  Received: { label: 'Received', tone: 'neutral' },
  Processing: { label: 'Processing', tone: 'info' },
  Processed: { label: 'Processed', tone: 'success' },
  Duplicate: { label: 'Duplicate', tone: 'outline' },
  Failed: { label: 'Failed', tone: 'danger' },
  Ignored: { label: 'Ignored', tone: 'outline' },
  Conflict: { label: 'Conflict', tone: 'danger' },
}

const reconciliationOutcomes: Record<string, StatusMeta> = {
  Matched: { label: 'Matched', tone: 'success' },
  PartiallyMatched: { label: 'Partially matched', tone: 'info' },
  AmountMismatch: { label: 'Amount mismatch', tone: 'warning' },
  Overpaid: { label: 'Overpaid', tone: 'warning' },
  Unmatched: { label: 'Unmatched', tone: 'warning' },
  Ambiguous: { label: 'Ambiguous', tone: 'warning' },
  ManuallyMatched: { label: 'Manual match', tone: 'info' },
}

const strategies: Record<string, string> = {
  ExactReference: 'Exact reference',
  CustomerAndAmount: 'Customer + amount',
  AmountAndDate: 'Amount + date',
  OrderId: 'Order id',
  Manual: 'Manual',
  None: 'No match',
}

const fallback = (value: string): StatusMeta => ({ label: value, tone: 'outline' })

export function orderStatusMeta(value: string): StatusMeta {
  return orderStatuses[value] ?? fallback(value)
}

export function paymentStatusMeta(value: string): StatusMeta {
  return paymentStatuses[value] ?? fallback(value)
}

export function webhookStatusMeta(value: string): StatusMeta {
  return webhookStatuses[value] ?? fallback(value)
}

export function reconciliationOutcomeMeta(value: string): StatusMeta {
  return reconciliationOutcomes[value] ?? fallback(value)
}

export function strategyLabel(value: string): string {
  return strategies[value] ?? value
}

export function actionLabel(action: string): string {
  return action.replace(/\./g, ' · ')
}

export function moduleLabel(module: string): string {
  const labels: Record<string, string> = {
    orders: 'Orders',
    payments: 'Payments',
    webhooks: 'Webhooks',
    reconciliation: 'Reconciliation',
    audit: 'Audit',
  }

  return labels[module] ?? module
}
