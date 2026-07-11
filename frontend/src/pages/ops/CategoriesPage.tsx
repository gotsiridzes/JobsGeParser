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
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Categories</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Scrape targets with job counts and latest run health
        </p>
      </div>

      {!data?.length && <EmptyState message="No categories configured" />}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {data?.map((category) => (
          <Card key={category.slug}>
            <CardHeader>
              <div className="flex items-start justify-between gap-2">
                <div>
                  <CardTitle className="text-base">{category.name}</CardTitle>
                  <CardDescription className="font-mono">{category.slug}</CardDescription>
                </div>
                {category.enabled ? (
                  <Badge variant="success">Enabled</Badge>
                ) : (
                  <Badge variant="muted">Disabled</Badge>
                )}
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-2xl font-bold">{category.jobCount}</p>
              <p className="text-xs text-muted-foreground">jobs in database</p>

              {category.latestScrapeRun ? (
                <div className="border-t pt-3 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs text-muted-foreground">Latest scrape</span>
                    <StatusBadge status={category.latestScrapeRun.status} />
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {formatDateTime(category.latestScrapeRun.finishedAt ?? category.latestScrapeRun.startedAt)}
                  </p>
                  <RunCounters run={category.latestScrapeRun} />
                  {category.latestScrapeRun.errorMessage && (
                    <p className="text-xs text-destructive truncate">
                      {category.latestScrapeRun.errorMessage}
                    </p>
                  )}
                </div>
              ) : (
                <p className="text-xs text-muted-foreground border-t pt-3">No scrape runs yet</p>
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
