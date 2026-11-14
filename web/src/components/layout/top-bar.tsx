import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import {
  ArrowRight,
  BarChart3,
  Building2,
  ChevronDown,
  ChevronRight,
  CircleHelp,
  CreditCard,
  LayoutDashboard,
  LifeBuoy,
  LogOut,
  Menu,
  ScrollText,
  Search,
  ShoppingCart,
  User,
  Users,
  Wallet,
  Webhook,
  type LucideIcon,
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { BrandMark } from '@/components/brand/brand-mark'
import { SidebarNav } from '@/components/layout/app-sidebar'
import { initials } from '@/lib/format'
import { usePermissions } from '@/lib/permissions'
import { useSession } from '@/lib/session'
import { signOut } from '@/lib/sign-out'
import type { TenantRole } from '@/lib/roles'

type CommandPage = { label: string; to: string; icon: LucideIcon; roles?: TenantRole[] }

const pages: CommandPage[] = [
  { label: 'Dashboard', to: '/dashboard', icon: LayoutDashboard },
  { label: 'Orders', to: '/orders', icon: ShoppingCart },
  { label: 'Payments', to: '/payments', icon: CreditCard },
  { label: 'Reconciliation', to: '/reconciliation', icon: LayoutDashboard },
  { label: 'Reports', to: '/reports', icon: BarChart3 },
  { label: 'Webhooks', to: '/webhooks', icon: Webhook },
  { label: 'Audit history', to: '/audit', icon: ScrollText },
  { label: 'Team Members', to: '/settings/team', icon: Users, roles: ['Owner', 'TenantAdmin'] },
  { label: 'Subscription & Usage', to: '/settings/subscription', icon: Wallet },
  { label: 'Workspace Settings', to: '/settings/workspace', icon: Building2, roles: ['Owner', 'TenantAdmin'] },
  { label: 'My Account', to: '/settings/profile', icon: User },
  { label: 'Help & Support', to: '/support', icon: LifeBuoy },
]

const sectionLabels: Record<string, string> = {
  '': 'Dashboard',
  dashboard: 'Dashboard',
  orders: 'Orders',
  payments: 'Payments',
  reconciliation: 'Reconciliation',
  webhooks: 'Webhooks',
  audit: 'Audit history',
  reports: 'Reports',
  settings: 'Settings',
  support: 'Support',
}

const settingsLabels: Record<string, string> = {
  profile: 'My account',
  team: 'Team members',
  subscription: 'Subscription & usage',
  workspace: 'Workspace settings',
}

function detailLabelFor(segments: string[]): string {
  const root = segments[0]

  if (root === 'orders') return 'Order detail'
  if (root === 'payments') return 'Payment detail'
  if (root === 'settings') return settingsLabels[segments[1]] ?? 'Settings'
  if (root === 'support') {
    if (segments[1] !== 'tickets') return 'Support'
    return segments[2] ? 'Ticket detail' : 'Tickets'
  }

  return 'Detail'
}

function Breadcrumbs() {
  const { pathname } = useLocation()
  const segments = pathname.split('/').filter(Boolean)

  if (segments.length === 0) {
    return <span className="text-sm font-medium text-foreground">Dashboard</span>
  }

  const root = segments[0]
  const rootLabel = sectionLabels[root] ?? root
  const detailLabel = detailLabelFor(segments)

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
  const { role } = usePermissions()

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

  const visiblePages = pages.filter((page) => !page.roles || (role && page.roles.includes(role)))

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
            {visiblePages.map((page) => (
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

function UserMenu() {
  const session = useSession()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [signingOut, setSigningOut] = useState(false)

  if (!session) return null

  const displayName = session.user.displayName || session.user.email
  const handleSignOut = async () => {
    if (signingOut) return
    setSigningOut(true)
    await signOut(queryClient)
    navigate('/sign-in', { replace: true })
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-2 rounded-lg border bg-background px-1.5 py-1 text-left transition-colors hover:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
          aria-label="Open user menu"
        >
          <Avatar className="size-6">
            <AvatarFallback className="bg-primary/10 text-[10px] font-semibold text-primary">
              {initials(displayName)}
            </AvatarFallback>
          </Avatar>
          <span className="hidden max-w-[140px] truncate pr-1 text-xs font-medium sm:inline">{displayName}</span>
          <ChevronDown className="hidden size-3.5 text-muted-foreground sm:block" aria-hidden />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuLabel className="space-y-0.5">
          <p className="truncate text-sm font-medium text-foreground">{displayName}</p>
          <p className="truncate text-xs font-normal text-muted-foreground">{session.user.email}</p>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to="/settings/profile" className="cursor-pointer">
            <User className="size-4" aria-hidden />
            My Account
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link to="/support" className="cursor-pointer">
            <LifeBuoy className="size-4" aria-hidden />
            Help &amp; Support
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onSelect={() => void handleSignOut()}
          disabled={signingOut}
          className="cursor-pointer text-destructive focus:text-destructive"
        >
          <LogOut className="size-4" aria-hidden />
          {signingOut ? 'Signing out…' : 'Sign Out'}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

export function TopBar() {
  const [navOpen, setNavOpen] = useState(false)

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b bg-card px-4 lg:px-6">
      <Sheet open={navOpen} onOpenChange={setNavOpen}>
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
          <SidebarNav onNavigate={() => setNavOpen(false)} />
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
            <Button variant="ghost" size="icon" className="text-muted-foreground" asChild>
              <Link to="/support" aria-label="Help and support">
                <CircleHelp className="size-[18px]" aria-hidden />
              </Link>
            </Button>
          </TooltipTrigger>
          <TooltipContent>Open Help &amp; Support</TooltipContent>
        </Tooltip>

        <UserMenu />
      </div>
    </header>
  )
}
