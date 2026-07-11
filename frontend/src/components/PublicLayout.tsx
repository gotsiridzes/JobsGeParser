import { Link, Outlet } from 'react-router-dom'
import { AttributionFooter } from '@/components/AttributionFooter'

export function PublicLayout() {
  return (
    <div className="min-h-screen flex flex-col bg-background public-shell">
      <header className="border-b border-border/60 bg-background/80 backdrop-blur-sm sticky top-0 z-20">
        <div className="mx-auto max-w-3xl px-4 h-14 flex items-center justify-between">
          <Link to="/" className="font-display text-lg tracking-tight text-foreground">
            JobsGe
          </Link>
          <Link
            to="/ops"
            className="text-xs text-muted-foreground hover:text-foreground transition-colors"
          >
            Ops
          </Link>
        </div>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
      <AttributionFooter />
    </div>
  )
}
