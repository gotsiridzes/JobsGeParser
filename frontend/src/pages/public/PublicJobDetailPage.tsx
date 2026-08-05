import { Link, useLocation, useParams } from 'react-router-dom'
import { ExternalLink } from 'lucide-react'
import { useJob } from '@/api/hooks'
import { JobStructuredChips } from '@/components/JobStructuredChips'
import { ErrorState, LoadingState } from '@/components/StateViews'
import { Button } from '@/components/ui/input'
import { formatDateOnly, toJobsGeUrl } from '@/lib/utils'

type SearchNavState = { q?: string; category?: string; page?: number }

export function PublicJobDetailPage() {
  const { id } = useParams()
  const location = useLocation()
  const jobId = Number(id)
  const { data, isLoading, isError, error, refetch } = useJob(jobId)
  const navState = (location.state as SearchNavState | null) ?? null

  const backTo = (() => {
    if (!navState) return '/search'
    const sp = new URLSearchParams()
    if (navState.q) sp.set('q', navState.q)
    if (navState.category) sp.set('category', navState.category)
    if (navState.page && navState.page > 1) sp.set('page', String(navState.page))
    const qs = sp.toString()
    return qs ? `/search?${qs}` : '/search'
  })()

  if (isLoading) return <LoadingState />
  if (isError) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-10">
        <ErrorState
          message={error instanceof Error ? error.message : 'Failed to load job'}
          onRetry={() => refetch()}
        />
      </div>
    )
  }
  if (!data) return null

  return (
    <div className="mx-auto max-w-3xl px-4 py-8 space-y-6">
      <Link to={backTo} className="text-sm text-muted-foreground hover:text-foreground">
        ← Back to results
      </Link>

      <div className="space-y-3">
        <h1 className="font-display text-3xl tracking-tight text-foreground">{data.name}</h1>
        <p className="text-lg text-muted-foreground">{data.company}</p>
        <JobStructuredChips
          salaryMin={data.salaryMin}
          salaryMax={data.salaryMax}
          salaryCurrency={data.salaryCurrency}
          salaryPeriod={data.salaryPeriod}
          city={data.city}
          workMode={data.workMode}
          employmentType={data.employmentType}
          seniority={data.seniority}
          languageRequirement={data.languageRequirement}
          categorySlugs={data.categorySlugs}
        />
        <p className="text-sm text-muted-foreground">
          Posted {formatDateOnly(data.published)}
          {data.endDate ? ` · Ends ${formatDateOnly(data.endDate)}` : ''}
        </p>
      </div>

      <a href={toJobsGeUrl(data.link)} target="_blank" rel="noopener noreferrer">
        <Button className="w-full sm:w-auto h-11 gap-2 bg-[hsl(var(--public-accent))] text-white hover:opacity-90">
          View on jobs.ge
          <ExternalLink className="h-4 w-4" />
        </Button>
      </a>

      <section className="pt-2">
        <h2 className="font-display text-lg tracking-tight mb-3">Description</h2>
        {data.description ? (
          <div className="whitespace-pre-wrap text-sm leading-relaxed text-foreground/90">
            {data.description}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">Description not available yet.</p>
        )}
      </section>
    </div>
  )
}
