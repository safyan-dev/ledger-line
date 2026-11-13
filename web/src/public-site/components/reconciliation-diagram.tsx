export function ReconciliationDiagram({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 720 260"
      role="img"
      aria-label="Orders and payments flow into a reconciliation engine, which classifies each record as matched, unmatched or an amount mismatch."
      className={className}
    >
      <defs>
        <marker id="flow-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
          <path d="M 0 0 L 10 5 L 0 10 z" className="fill-primary/60" />
        </marker>
      </defs>

      <g className="fill-card stroke-border" strokeWidth="1.5">
        <rect x="16" y="34" width="168" height="62" rx="12" />
        <rect x="16" y="164" width="168" height="62" rx="12" />
        <rect x="286" y="88" width="168" height="84" rx="14" />
        <rect x="536" y="26" width="168" height="52" rx="12" />
        <rect x="536" y="104" width="168" height="52" rx="12" />
        <rect x="536" y="182" width="168" height="52" rx="12" />
      </g>

      <g className="fill-foreground" fontSize="13" fontWeight="600">
        <text x="100" y="60" textAnchor="middle">Orders</text>
        <text x="100" y="190" textAnchor="middle">Payments</text>
        <text x="370" y="122" textAnchor="middle">Reconciliation</text>
        <text x="370" y="142" textAnchor="middle">engine</text>
        <text x="620" y="57" textAnchor="middle" fontSize="12">Matched</text>
        <text x="620" y="135" textAnchor="middle" fontSize="12">Unmatched</text>
        <text x="620" y="213" textAnchor="middle" fontSize="12">Amount mismatch</text>
      </g>

      <g className="fill-muted-foreground" fontSize="11">
        <text x="100" y="78" textAnchor="middle">expected amount</text>
        <text x="100" y="208" textAnchor="middle">provider amount</text>
        <text x="370" y="160" textAnchor="middle">reference · customer · date</text>
      </g>

      <g className="stroke-success" strokeWidth="8" strokeLinecap="round">
        <line x1="552" y1="52" x2="688" y2="52" opacity="0.25" />
      </g>
      <g className="stroke-warning" strokeWidth="8" strokeLinecap="round">
        <line x1="552" y1="130" x2="688" y2="130" opacity="0.25" />
      </g>
      <g className="stroke-destructive" strokeWidth="8" strokeLinecap="round">
        <line x1="552" y1="208" x2="688" y2="208" opacity="0.25" />
      </g>

      <g
        className="stroke-primary fill-none motion-safe:animate-[public-flow_1.6s_linear_infinite] motion-reduce:animate-none"
        strokeWidth="1.75"
        strokeDasharray="6 6"
        markerEnd="url(#flow-arrow)"
      >
        <path d="M 184 65 C 236 65, 254 110, 286 118" />
        <path d="M 184 195 C 236 195, 254 150, 286 142" />
        <path d="M 454 112 C 500 92, 508 56, 536 52" />
        <path d="M 454 130 C 500 130, 508 130, 536 130" />
        <path d="M 454 148 C 500 168, 508 204, 536 208" />
      </g>

      <g>
        <circle cx="184" cy="65" r="4" className="fill-primary" />
        <circle cx="184" cy="195" r="4" className="fill-primary" />
        <circle cx="454" cy="112" r="4" className="fill-success" />
        <circle cx="454" cy="130" r="4" className="fill-warning" />
        <circle cx="454" cy="148" r="4" className="fill-destructive" />
      </g>
    </svg>
  )
}
