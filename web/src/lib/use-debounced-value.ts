import { useEffect, useState } from 'react'

// Returns a value that only updates once the input has been quiet for `delay`.
// Used to keep typing responsive while limiting how often a search re-queries.
export function useDebouncedValue<T>(value: T, delay = 250): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])

  return debounced
}
