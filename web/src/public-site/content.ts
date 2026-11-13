import {
  BadgeCheck,
  CircleAlert,
  Copy,
  GitCompareArrows,
  Layers,
  LineChart,
  ScrollText,
  ShoppingCart,
  CreditCard,
  Users,
  Webhook,
  type LucideIcon,
} from 'lucide-react'

export type Problem = {
  title: string
  description: string
}

export type Capability = {
  key: string
  title: string
  problem: string
  solution: string
  value: string
  icon: LucideIcon
}

export type Step = {
  title: string
  description: string
}

export const problems: Problem[] = [
  {
    title: 'Payments arrive in fragments',
    description:
      'Every provider names the same transaction differently, so finance teams cannot tell which payment belongs to which order.',
  },
  {
    title: 'Duplicates quietly inflate the books',
    description:
      'Webhooks are retried by design. Without deduplication, one payment can look like two and revenue is overstated.',
  },
  {
    title: 'Reconciliation happens too late',
    description:
      'Spreadsheet matching runs weekly or monthly. By the time a gap is found, the customer and the order are already cold.',
  },
  {
    title: 'Nobody can prove what changed',
    description:
      'Manual adjustments leave no consistent trail, so audits and month-end reviews turn into archaeology.',
  },
]

export const capabilities: Capability[] = [
  {
    key: 'payment-tracking',
    title: 'Payment tracking',
    problem: 'Payments are scattered across provider dashboards with no shared view.',
    solution: 'Every transaction is recorded once with its provider identifiers, amount, currency and status.',
    value: 'Finance sees one authoritative list instead of logging into four providers.',
    icon: CreditCard,
  },
  {
    key: 'order-management',
    title: 'Order management',
    problem: 'Orders live in one system and money lands in another, with no reliable link between them.',
    solution: 'Orders track expected, paid and remaining amounts, and every allocation is recorded.',
    value: 'Operators know exactly how much is still owed on each order.',
    icon: ShoppingCart,
  },
  {
    key: 'webhook-processing',
    title: 'Webhook processing',
    problem: 'Provider events are the source of truth but arrive as noisy, unsigned payloads.',
    solution: 'Signed events are verified, stored raw and processed into payment state with a full correlation trail.',
    value: 'Payment status updates itself, without a human copying values across systems.',
    icon: Webhook,
  },
  {
    key: 'duplicate-handling',
    title: 'Duplicate event handling',
    problem: 'Retried deliveries create the same payment twice and distort totals.',
    solution: 'Event identity and payload hashes are enforced, so a replay is counted as a duplicate, not a new payment.',
    value: 'Numbers stay accurate even when a provider retries aggressively.',
    icon: Copy,
  },
  {
    key: 'reconciliation',
    title: 'Payment reconciliation',
    problem: 'Matching is manual, inconsistent and impossible to explain after the fact.',
    solution: 'A deterministic engine matches on reference, customer, amount and date, then records the strategy it used.',
    value: 'Every match can be explained, reviewed and repeated.',
    icon: GitCompareArrows,
  },
  {
    key: 'exceptions',
    title: 'Matched, unmatched and mismatched',
    problem: 'Exceptions are buried among successful payments until someone goes looking.',
    solution: 'Results are classified as matched, unmatched or amount mismatch, and surfaced as a review queue.',
    value: 'The team works the exceptions first instead of scanning everything.',
    icon: CircleAlert,
  },
  {
    key: 'audit-history',
    title: 'Audit history',
    problem: 'Manual fixes are untraceable, so nobody can answer who changed what.',
    solution: 'Every business action writes an audit row with actor, module, entity and correlation id.',
    value: 'Month-end and external audits are a lookup, not an investigation.',
    icon: ScrollText,
  },
  {
    key: 'reporting',
    title: 'Financial visibility and reporting',
    problem: 'Leadership sees totals without knowing how much is still unresolved.',
    solution: 'The dashboard reports matched coverage, unmatched value and payments awaiting a decision.',
    value: 'Decisions are made from current payment state, not last week’s export.',
    icon: LineChart,
  },
  {
    key: 'collaboration',
    title: 'Team collaboration',
    problem: 'One person owns the spreadsheet, and the rest of the team is blind.',
    solution: 'Shared roles, a common review queue and recorded manual matches keep the team working from one record.',
    value: 'Work is shared without losing accountability.',
    icon: Users,
  },
]

export const steps: Step[] = [
  {
    title: 'Orders enter the system',
    description: 'An order records what the customer should pay, in which currency, and how to recognise it later.',
  },
  {
    title: 'Payment transactions are recorded',
    description: 'Each provider payment is stored once with its amount, reference and provider identifiers.',
  },
  {
    title: 'Webhooks update payment information',
    description: 'Signed provider events move payments through received, failed and refunded states.',
  },
  {
    title: 'The reconciliation engine compares',
    description: 'Orders and payments are compared on reference, customer, amount and date, using recorded strategies.',
  },
  {
    title: 'Records are classified',
    description: 'Each result becomes matched, unmatched or an amount mismatch, with the difference preserved.',
  },
  {
    title: 'Finance reviews the exceptions',
    description: 'The team works the review queue, resolves mismatches and every decision lands in the audit trail.',
  },
]

export const capabilityHighlights = [
  { icon: BadgeCheck, label: 'Deterministic matching' },
  { icon: Layers, label: 'One record per payment' },
  { icon: ScrollText, label: 'Auditable decisions' },
]
