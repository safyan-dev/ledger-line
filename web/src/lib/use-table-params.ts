import { useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'

type ParamUpdates = Record<string, string | number | undefined | null>

export function useTableParams(defaultPageSize = 25) {
  const [params, setParams] = useSearchParams()

  const page = Number(params.get('page') ?? '1') || 1
  const pageSize = Number(params.get('pageSize') ?? String(defaultPageSize)) || defaultPageSize

  const get = useCallback((key: string) => params.get(key) ?? '', [params])

  const update = useCallback(
    (updates: ParamUpdates, resetPage = true) => {
      const next = new URLSearchParams(params)

      for (const [key, value] of Object.entries(updates)) {
        if (value === undefined || value === null || value === '') {
          next.delete(key)
        } else {
          next.set(key, String(value))
        }
      }

      if (resetPage) {
        next.delete('page')
      }

      setParams(next)
    },
    [params, setParams],
  )

  const setPage = useCallback(
    (nextPage: number) => {
      const next = new URLSearchParams(params)
      next.set('page', String(Math.max(1, nextPage)))
      setParams(next)
    },
    [params, setParams],
  )

  const setPageSize = useCallback(
    (nextPageSize: number) => {
      const next = new URLSearchParams(params)
      next.set('pageSize', String(nextPageSize))
      next.delete('page')
      setParams(next)
    },
    [params, setParams],
  )

  return { params, page, pageSize, get, update, setPage, setPageSize }
}
