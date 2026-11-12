import type { ReactNode } from 'react'
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

export type DataTableColumn<T> = {
  id: string
  header: ReactNode
  className?: string
  cell: (row: T) => ReactNode
}

export function DataTable<T>({
  columns,
  rows,
  getRowKey,
  caption,
  isLoading = false,
  skeletonRows = 6,
  onRowClick,
  empty,
  className,
}: {
  columns: DataTableColumn<T>[]
  rows: T[]
  getRowKey: (row: T) => string
  caption: string
  isLoading?: boolean
  skeletonRows?: number
  onRowClick?: (row: T) => void
  empty?: ReactNode
  className?: string
}) {
  return (
    <div
      className={cn(
        'overflow-hidden rounded-xl border border-border/80 bg-card shadow-[0_1px_2px_rgba(15,23,42,0.04)]',
        className,
      )}
    >
      <div className="overflow-x-auto">
        <Table>
          <TableCaption className="sr-only">{caption}</TableCaption>
          <TableHeader>
            <TableRow className="border-border/80 hover:bg-transparent">
              {columns.map((column) => (
                <TableHead
                  key={column.id}
                  className={cn('h-10 bg-muted/40 px-3 text-[12px] font-medium text-muted-foreground sm:px-4', column.className)}
                >
                  {column.header}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: skeletonRows }).map((_, index) => (
                  <TableRow key={`skeleton-${index}`} className="border-border/70 hover:bg-transparent">
                    {columns.map((column) => (
                      <TableCell key={column.id} className={cn('px-3 py-3 sm:px-4', column.className)}>
                        <Skeleton className="h-4 w-full max-w-[140px]" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              : rows.map((row) => (
                  <TableRow
                    key={getRowKey(row)}
                    onClick={onRowClick ? () => onRowClick(row) : undefined}
                    onKeyDown={
                      onRowClick
                        ? (event) => {
                            const target = event.target as HTMLElement
                            if (target.closest('a, button, input, select, textarea, [role="button"]')) return
                            if (event.key === 'Enter' || event.key === ' ') {
                              event.preventDefault()
                              onRowClick(row)
                            }
                          }
                        : undefined
                    }
                    tabIndex={onRowClick ? 0 : undefined}
                    className={cn(
                      'border-border/70',
                      onRowClick
                        ? 'cursor-pointer hover:bg-muted/40 focus-visible:bg-muted/40 focus-visible:outline-none'
                        : 'hover:bg-transparent',
                    )}
                  >
                    {columns.map((column) => (
                      <TableCell key={column.id} className={cn('px-3 py-3 align-middle sm:px-4', column.className)}>
                        {column.cell(row)}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
            {!isLoading && rows.length === 0 ? (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={columns.length} className="p-0">
                  {empty}
                </TableCell>
              </TableRow>
            ) : null}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}
