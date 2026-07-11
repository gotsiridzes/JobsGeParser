import { FormEvent, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Search } from 'lucide-react'
import { useCategories, useJobs } from '@/api/hooks'
import { JobResultCard } from '@/components/JobResultCard'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Button, Input } from '@/components/ui/input'

export function HomePage() {
  const navigate = useNavigate()
  const [q, setQ] = useState('')
  const { data: categories } = useCategories()
  const latest = useJobs({ page: 1, pageSize: 8 })

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    const trimmed = q.trim()
    if (!trimmed) return
    navigate(`/search?q=${encodeURIComponent(trimmed)}`)
  }

  const enabledCategories = categories?.filter((c) => c.enabled) ?? []

  return (
    <div>
      <section className="public-hero relative overflow-hidden">
        <div className="absolute inset-0 public-hero-atmosphere pointer-events-none" aria-hidden />
        <div className="relative mx-auto max-w-3xl px-4 pt-16 pb-12 sm:pt-24 sm:pb-16">
          <p className="font-display text-4xl sm:text-5xl tracking-tight text-foreground mb-3">
            JobsGe
          </p>
          <h1 className="text-xl sm:text-2xl font-medium text-foreground/90 max-w-xl">
            Search what is actually in the job
          </h1>
          <p className="mt-3 text-sm sm:text-base text-muted-foreground max-w-lg">
            Description-level search across jobs.ge categories, with salary, remote, and seniority
            when the posting mentions them.
          </p>

          <form onSubmit={onSubmit} className="mt-8 flex gap-2">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="e.g. senior backend remote"
                className="pl-9 h-12 text-base bg-background/90"
                autoFocus
              />
            </div>
            <Button type="submit" className="h-12 px-5 bg-[hsl(var(--public-accent))] text-white hover:opacity-90">
              Search
            </Button>
          </form>

          {enabledCategories.length > 0 && (
            <div className="mt-6 flex flex-wrap gap-2">
              {enabledCategories.map((c) => (
                <Link
                  key={c.slug}
                  to={`/search?category=${encodeURIComponent(c.slug)}`}
                  className="text-xs rounded-full border border-border/80 px-3 py-1.5 text-muted-foreground hover:text-foreground hover:border-foreground/30 transition-colors"
                >
                  {c.name}
                </Link>
              ))}
            </div>
          )}
        </div>
      </section>

      <section className="mx-auto max-w-3xl px-4 py-10">
        <h2 className="font-display text-xl tracking-tight mb-4">Latest jobs</h2>
        {latest.isLoading && <LoadingState />}
        {latest.isError && (
          <ErrorState
            message={latest.error instanceof Error ? latest.error.message : 'Failed to load jobs'}
            onRetry={() => latest.refetch()}
          />
        )}
        {latest.data && latest.data.items.length === 0 && (
          <EmptyState message="No jobs scraped yet" />
        )}
        {latest.data && latest.data.items.length > 0 && (
          <div>
            {latest.data.items.map((job) => (
              <JobResultCard key={job.id} job={job} />
            ))}
          </div>
        )}
      </section>
    </div>
  )
}
