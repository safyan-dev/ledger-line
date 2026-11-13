import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import {
  ArrowRight,
  ChevronRight,
  CircleHelp,
  CreditCard,
  LayoutDashboard,
  Menu,
  ScrollText,
  Search,
  ShoppingCart,
  Webhook,
} from 'lucide-react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from '@/components/ui/command'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { BrandMark } from '@/components/brand/brand-mark'
import { SidebarNav } from '@/components/layout/app-sidebar'
import { initials } from '@/lib/format'

const pages = [
  { label: 'Dashboard', to: '/dashboard', icon: LayoutDashboard },
  { label: 'Orders', to: '/orders', icon: ShoppingCart },
  { label: 'Payments', to: '/payments', icon: CreditCard },
  { label: 'Reconciliation', to: '/reconciliation', icon: LayoutDashboard },
  { label: 'Webhooks', to: '/webhooks', icon: Webhook },
  { label: 'Audit history', to: '/audit', icon: ScrollText },
]

const sectionLabels: Record<string, string> = {
  '': 'Dashboard',
  dashboard: 'Dashboard',
  orders: 'Orders',
  payments: 'Payments',
  reconciliation: 'Reconciliation',
  webhooks: 'Webhooks',
  audit: 'Audit history',
}

function Breadcrumbs() {
  const { pathname } = useLocation()
  const segments = pathname.split('/').filter(Boolean)

  if (segments.length === 0) {
    return <span className="text-sm font-medium text-foreground">Dashboard</span>
  }

  const root = segments[0]
  const rootLabel = sectionLabels[root] ?? root
  const detailLabel = root === 'orders' ? 'Order detail' : root === 'payments' ? 'Payment detail' : 'Detail'

  return (
    <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-1.5 text-xs">
      <Link to="/dashboard" className="shrink-0 text-muted-foreground transition-colors hover:text-foreground">
        Ledgerline
      </Link>
      <ChevronRight className="size-3 shrink-0 text-muted-foreground/50" aria-hidden />
      {segments.length === 1 ? (
        <span className="truncate font-medium text-foreground">{rootLabel}</span>
      ) : (
        <>
          <Link to={`/${root}`} className="truncate text-muted-foreground transition-colors hover:text-foreground">
            {rootLabel}
          </Link>
          <ChevronRight className="size-3 shrink-0 text-muted-foreground/50" aria-hidden />
          <span className="truncate font-medium text-foreground">{detailLabel}</span>
        </>
      )}
    </nav>
  )
}

function CommandMenu() {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const navigate = useNavigate()

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault()
        setOpen((value) => !value)
      }
    }

    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [])

  const go = (path: string) => {
    setOpen(false)
    setQuery('')
    navigate(path)
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="group flex h-9 min-w-0 max-w-sm flex-1 items-center gap-2 rounded-lg border bg-background px-3 text-sm text-muted-foreground transition-colors hover:border-primary/30 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
        aria-label="Search orders, payments and references"
      >
        <Search className="size-4" aria-hidden />
        <span className="truncate">Search orders, payments, references…</span>
        <kbd className="ml-auto hidden rounded border bg-muted px-1.5 py-0.5 font-sans text-[10px] font-medium text-muted-foreground sm:inline">
          ⌘K
        </kbd>
      </button>

      <CommandDialog open={open} onOpenChange={setOpen}>
        <CommandInput placeholder="Search pages or jump to a record…" value={query} onValueChange={setQuery} />
        <CommandList>
          <CommandEmpty>No results found.</CommandEmpty>
          <CommandGroup heading="Pages">
            {pages.map((page) => (
              <CommandItem key={page.to} value={page.label} onSelect={() => go(page.to)}>
                <page.icon className="size-4 text-muted-foreground" aria-hidden />
                {page.label}
              </CommandItem>
            ))}
          </CommandGroup>
          <CommandSeparator />
          <CommandGroup heading="Search">
            <CommandItem value="search orders" onSelect={() => go(`/orders?search=${encodeURIComponent(query)}`)}>
              <Search className="size-4 text-muted-foreground" aria-hidden />
              Search orders for “{query || '…'}”
              <ArrowRight className="ml-auto size-3.5 text-muted-foreground" aria-hidden />
            </CommandItem>
            <CommandItem value="search payments" onSelect={() => go(`/payments?search=${encodeURIComponent(query)}`)}>
              <Search className="size-4 text-muted-foreground" aria-hidden />
              Search payments for “{query || '…'}”
              <ArrowRight className="ml-auto size-3.5 text-muted-foreground" aria-hidden />
            </CommandItem>
            <CommandItem value="unmatched payments" onSelect={() => go('/payments?status=Unmatched')}>
              <Search className="size-4 text-muted-foreground" aria-hidden />
              Review unmatched payments
            </CommandItem>
          </CommandGroup>
        </CommandList>
      </CommandDialog>
    </>
  )
}

export function TopBar() {
  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b bg-card px-4 lg:px-6">
      <Sheet>
        <SheetTrigger asChild>
          <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Open navigation">
            <Menu className="size-5" aria-hidden />
          </Button>
        </SheetTrigger>
        <SheetContent side="left" className="w-72 p-0">
          <SheetHeader className="border-b px-5 py-4">
            <SheetTitle asChild>
              <div>
                <BrandMark />
              </div>
            </SheetTitle>
          </SheetHeader>
          <SidebarNav />
        </SheetContent>
      </Sheet>

      <div className="hidden min-w-0 flex-1 items-center lg:flex">
        <Breadcrumbs />
      </div>

      <CommandMenu />

      <div className="ml-auto flex items-center gap-2 sm:gap-3">
        <span className="hidden items-center gap-1.5 rounded-md border bg-background px-2.5 py-1 text-xs font-medium text-muted-foreground sm:inline-flex">
          <span className="size-1.5 rounded-full bg-success" aria-hidden />
          Demo
        </span>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button variant="ghost" size="icon" className="text-muted-foreground" aria-label="Help">
              <CircleHelp className="size-[18px]" aria-hidden />
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            Reconciliation runs on signed provider webhooks. Contact your workspace administrator for provider setup or access.
          </TooltipContent>
        </Tooltip>

        <div className="flex items-center gap-2 rounded-lg border bg-background px-1.5 py-1">
          <Avatar className="size-6">
            <AvatarFallback className="bg-primary/10 text-[10px] font-semibold text-primary">
              {initials('Avery Chen')}
            </AvatarFallback>
          </Avatar>
          <span className="hidden pr-1 text-xs font-medium sm:inline">Avery Chen</span>
        </div>
      </div>
    </header>
  )
}
