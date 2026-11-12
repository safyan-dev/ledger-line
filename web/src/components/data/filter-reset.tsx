export function FilterReset({
  visible,
  onClick,
  label = 'Reset filters',
}: {
  visible: boolean
  onClick: () => void
  label?: string
}) {
  if (!visible) return null

  return (
    <button
      type="button"
      onClick={onClick}
      className="h-9 rounded-md px-2 text-xs font-medium text-muted-foreground underline-offset-4 hover:text-foreground hover:underline"
    >
      {label}
    </button>
  )
}