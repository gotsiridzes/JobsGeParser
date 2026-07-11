import { FormEvent, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Search } from 'lucide-react'
import { useCategories, useJobs, useSearch } from '@/api/hooks'
import { JobResultCard } from '@/components/JobResultCard'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Button, Input, Select } from '@/components/ui/input'

const PAGE_SIZE = 20

export function SearchPage() {
  const [params, setParams] = useSearchParams()
  const navigate = useNavigate()
  const q = params.get('q')?.trim() ?? ''
  const category = params.get('category') ?? ''
  const page = Math.max(1, Number(params.get('page') ?? '1') || 1)

  const [input, setInput] = useState(q)
  const { data: categories } = useCategories()

  const isBrowse = !q && !!category
  const searchState = useMemo(() => ({ q: q || undefined, category: category || undefined, page }), [q, category, page])

  const searchQuery = useSearch({
    q,
    category: category || undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  const browseQuery = useJobs({
    category: category || undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  const active = isBrowse ? browseQuery : searchQuery
  const items = isBrowse
    ? browseQuery.data?.items ?? []
    : searchQuery.data?.items ?? []
  const totalCount = isBrowse
    ? browseQuery.data?.totalCount ?? 0
    : searchQuery.data?.totalCount ?? 0
  const totalPages = isBrowse
    ? browseQuery.data?.totalPages ?? 0
    : searchQuery.data?.totalPages ?? 0
  const mode = isBrowse ? 'browse' : searchQuery.data?.mode

  function updateParams(next: { q?: string; category?: string; page?: number }) {
    const sp = new URLSearchParams()
    const nextQ = next.q !== undefined ? next.q : q
    const nextCat = next.category !== undefined ? next.category : category
    const nextPage = next.page !== undefined ? next.page : 1
    if (nextQ) sp.set('q', nextQ)
    if (nextCat) sp.set('category', nextCat)
    if (nextPage > 1) sp.set('page', String(nextPage))
    setParams(sp)
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    const trimmed = input.trim()
    if (!trimmed && !category) return
    updateParams({ q: trimmed, page: 1 })
  }

  if (!q && !category) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-10 space-y-6">
        <h1 className="font-display text-2xl tracking-tight">Search</h1>
        <form onSubmit={onSubmit} className="flex gap-2">
          <Input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Search job text…"
            className="h-11"
            autoFocus
          />
          <Button type="submit" className="h-11">
            Search
          </Button>
        </form>
        <EmptyState message="Enter a query or pick a category from the home page" />
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-8 space-y-6">
      <form onSubmit={onSubmit} className="flex flex-col sm:flex-row gap-2 sticky top-14 z-10 bg-background/95 py-3 backdrop-blur-sm border-b border-border/50 -mx-4 px-4">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Search job text…"
            className="pl-9 h-11"
          />
        </div>
        <Select
          value={category}
          onChange={(e) => {
            setInput(input)
            updateParams({ category: e.target.value, page: 1 })
          }}
          className="h-11 sm:w-44"
        >
          <option value="">All categories</option>
          {categories?.map((c) => (
            <option key={c.slug} value={c.slug}>
              {c.name}
            </option>
          ))}
        </Select>
        <Button type="submit" className="h-11">
          Search
        </Button>
      </form>

      <div className="flex items-baseline justify-between gap-2">
        <div>
          <h1 className="font-display text-xl tracking-tight">
            {isBrowse ? `Browsing ${category}` : `Results for "${q}"`}
          </h1>
          <p className="text-xs text-muted-foreground mt-1">
            {totalCount} jobs
            {mode ? ` · ${mode}` : ''}
          </p>
        </div>
      </div>

      {active.isLoading && <LoadingState />}
      {active.isError && (
        <ErrorState
          message={active.error instanceof Error ? active.error.message : 'Search failed'}
          onRetry={() => active.refetch()}
        />
      )}

      {!active.isLoading && !active.isError && items.length === 0 && (
        <EmptyState message="No jobs matched. Try different words or clear the category filter." />
      )}

      {items.length > 0 && (
        <>
          <div>
            {items.map((job) => (
              <JobResultCard key={job.id} job={job} searchState={searchState} />
            ))}
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between pt-2">
              <p className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  disabled={page <= 1}
                  onClick={() => updateParams({ page: page - 1 })}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  disabled={page >= totalPages}
                  onClick={() => updateParams({ page: page + 1 })}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </>
      )}

      {!q && category && (
        <button
          type="button"
          className="text-xs text-muted-foreground hover:text-foreground"
          onClick={() => navigate('/')}
        >
          Back to home
        </button>
      )}
    </div>
  )
}
