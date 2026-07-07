import { Link, NavLink, Outlet } from 'react-router-dom'
import { Activity, Briefcase, FolderTree, History, LayoutDashboard } from 'lucide-react'
import { cn } from '@/lib/utils'

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/runs', label: 'Run History', icon: History },
  { to: '/categories', label: 'Categories', icon: FolderTree },
  { to: '/jobs', label: 'Jobs', icon: Briefcase },
]

export function Layout() {
  return (
    <div className="min-h-screen flex">
      <aside className="w-56 border-r bg-muted/30 flex flex-col">
        <div className="p-4 border-b">
          <Link to="/" className="flex items-center gap-2 font-semibold">
            <Activity className="h-5 w-5 text-primary" />
            JobsGeParser
          </Link>
          <p className="text-xs text-muted-foreground mt-1">Operations console</p>
        </div>
        <nav className="flex-1 p-3 space-y-1">
          {navItems.map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors',
                  isActive
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                )
              }
            >
              <Icon className="h-4 w-4" />
              {label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <main className="flex-1 overflow-auto">
        <div className="p-6 max-w-7xl mx-auto">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
