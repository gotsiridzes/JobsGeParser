import { useCategories } from '@/api/hooks'
import { RunCounters } from '@/components/RunCounters'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { formatDateTime } from '@/lib/utils'

export function CategoriesPage() {
  const { data, isLoading, isError, error, refetch } = useCategories()

  if (isLoading) return <LoadingState />
  if (isError) {
    return (
      <ErrorState
        message={error instanceof Error ? error.message : 'Failed to load categories'}
        onRetry={() => refetch()}
      />
    )
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Categories</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Scrape targets with job counts and latest run health
        </p>
      </div>

      {!data?.length && <EmptyState message="No categories configured" />}

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {data?.map((category) => (
          <Card key={category.slug}>
            <CardHeader className="p-3 pb-1.5 space-y-1">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <CardTitle className="text-sm truncate">{category.name}</CardTitle>
                  <CardDescription className="font-mono text-xs truncate">
                    {category.slug}
                  </CardDescription>
                </div>
                {category.enabled ? (
                  <Badge variant="success" className="shrink-0 text-[10px] px-1.5 py-0">
                    Enabled
                  </Badge>
                ) : (
                  <Badge variant="muted" className="shrink-0 text-[10px] px-1.5 py-0">
                    Disabled
                  </Badge>
                )}
              </div>
            </CardHeader>
            <CardContent className="p-3 pt-0 space-y-1.5">
              <p className="text-lg font-semibold leading-none">
                {category.jobCount}
                <span className="ml-1.5 text-xs font-normal text-muted-foreground">jobs</span>
              </p>

              {category.latestScrapeRun ? (
                <div className="border-t pt-1.5 space-y-1">
                  <div className="flex items-center justify-between gap-1">
                    <span className="text-[11px] text-muted-foreground">Latest</span>
                    <StatusBadge status={category.latestScrapeRun.status} />
                  </div>
                  <p className="text-[11px] text-muted-foreground">
                    {formatDateTime(
                      category.latestScrapeRun.finishedAt ?? category.latestScrapeRun.startedAt,
                    )}
                  </p>
                  <RunCounters run={category.latestScrapeRun} />
                  {category.latestScrapeRun.errorMessage && (
                    <p className="text-[11px] text-destructive truncate">
                      {category.latestScrapeRun.errorMessage}
                    </p>
                  )}
                </div>
              ) : (
                <p className="text-[11px] text-muted-foreground border-t pt-1.5">No scrape runs yet</p>
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
