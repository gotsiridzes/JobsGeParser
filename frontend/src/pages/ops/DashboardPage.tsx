import { Link } from 'react-router-dom'
import { useScrapeOverview } from '@/api/hooks'
import { RunCounters } from '@/components/RunCounters'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { formatDateTime, formatDuration } from '@/lib/utils'

export function DashboardPage() {
  const { data, isLoading, isError, error, refetch, dataUpdatedAt } = useScrapeOverview()

  if (isLoading) return <LoadingState />
  if (isError) {
    return (
      <ErrorState
        message={error instanceof Error ? error.message : 'Failed to load overview'}
        onRetry={() => refetch()}
      />
    )
  }
  if (!data) return <EmptyState message="No overview data" />

  const { worker, activeRuns, latestPerCategory, recentRuns, recentBatches } = data
  const snapshot = worker.worker
  const tickProgress =
    snapshot.categoriesInCurrentTick.length > 0
      ? `${snapshot.completedCategoriesInCurrentTick} / ${snapshot.categoriesInCurrentTick.length}`
      : '—'

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Dashboard</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Live scrape worker and recent activity
          </p>
        </div>
        <p className="text-xs text-muted-foreground">
          Updated {new Date(dataUpdatedAt).toLocaleTimeString()}
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Scraping</CardDescription>
            <CardTitle className="text-2xl">
              {worker.scrapeEnabled ? (
                <Badge variant="success">Enabled</Badge>
              ) : (
                <Badge variant="muted">Disabled</Badge>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            Interval: {worker.scrapeIntervalMinutes} min
            <br />
            On startup: {worker.scrapeOnStartup ? 'yes' : 'no'}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Current tick</CardDescription>
            <CardTitle className="text-2xl">
              {snapshot.isTickInProgress ? (
                <Badge variant="warning">In progress</Badge>
              ) : (
                <Badge variant="muted">Idle</Badge>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            Progress: {tickProgress}
            <br />
            Active categories: {snapshot.activeCategoryRuns.length}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Last tick</CardDescription>
            <CardTitle className="text-lg">
              {formatDateTime(snapshot.lastTickCompletedAt ?? snapshot.lastTickStartedAt)}
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            Started: {formatDateTime(snapshot.lastTickStartedAt)}
            <br />
            Skipped ticks: {snapshot.skippedTicks}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Active runs</CardDescription>
            <CardTitle className="text-2xl">{activeRuns.length}</CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            {snapshot.currentBatchId ? (
              <Link
                to={`/ops/batches/${snapshot.currentBatchId}`}
                className="text-primary hover:underline"
              >
                View current batch
              </Link>
            ) : (
              'No batch in progress'
            )}
          </CardContent>
        </Card>
      </div>

      {snapshot.activeCategoryRuns.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Active category runs</CardTitle>
            <CardDescription>Categories currently being scraped</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Category</TableHead>
                  <TableHead>Run ID</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {snapshot.activeCategoryRuns.map((run) => (
                  <TableRow key={`${run.categorySlug}-${run.runId}`}>
                    <TableCell className="font-medium">{run.categorySlug}</TableCell>
                    <TableCell>{run.runId}</TableCell>
                    <TableCell>
                      <StatusBadge status="Running" />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {activeRuns.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Running scrape runs</CardTitle>
            <CardDescription>Live counters from the database</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>ID</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Started</TableHead>
                  <TableHead>Duration</TableHead>
                  <TableHead>Counters</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {activeRuns.map((run) => (
                  <TableRow key={run.id}>
                    <TableCell>{run.id}</TableCell>
                    <TableCell>{run.categorySlug ?? '—'}</TableCell>
                    <TableCell>{formatDateTime(run.startedAt)}</TableCell>
                    <TableCell>{formatDuration(run.startedAt, run.finishedAt)}</TableCell>
                    <TableCell>
                      <RunCounters run={run} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Latest per category</CardTitle>
          <CardDescription>Most recent scrape run for each category</CardDescription>
        </CardHeader>
        <CardContent>
          {latestPerCategory.length === 0 ? (
            <EmptyState message="No scrape runs yet" />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Category</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Finished</TableHead>
                  <TableHead>Duration</TableHead>
                  <TableHead>Counters</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {latestPerCategory.map((run) => (
                  <TableRow key={run.id}>
                    <TableCell className="font-medium">{run.categorySlug ?? '—'}</TableCell>
                    <TableCell>
                      <StatusBadge status={run.status} />
                    </TableCell>
                    <TableCell>{formatDateTime(run.finishedAt)}</TableCell>
                    <TableCell>{formatDuration(run.startedAt, run.finishedAt)}</TableCell>
                    <TableCell>
                      <RunCounters run={run} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Recent batches</CardTitle>
          <CardDescription>Grouped scrape ticks</CardDescription>
        </CardHeader>
        <CardContent>
          {recentBatches.length === 0 ? (
            <EmptyState message="No batches yet" />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Batch</TableHead>
                  <TableHead>Started</TableHead>
                  <TableHead>Runs</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {recentBatches.map((batch) => (
                  <TableRow key={batch.batchId}>
                    <TableCell>
                      <Link
                        to={`/ops/batches/${batch.batchId}`}
                        className="text-primary hover:underline font-mono text-xs"
                      >
                        {batch.batchId.slice(0, 8)}…
                      </Link>
                    </TableCell>
                    <TableCell>{formatDateTime(batch.startedAt)}</TableCell>
                    <TableCell>{batch.totalRuns}</TableCell>
                    <TableCell>
                      <span className="text-xs text-muted-foreground">
                        {batch.running > 0 && (
                          <Badge variant="warning" className="mr-1">
                            {batch.running} running
                          </Badge>
                        )}
                        <Badge variant="success" className="mr-1">
                          {batch.completed} done
                        </Badge>
                        {batch.failed > 0 && (
                          <Badge variant="destructive">{batch.failed} failed</Badge>
                        )}
                      </span>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Recent runs</CardTitle>
          <CardDescription>
            <Link to="/ops/runs" className="text-primary hover:underline">
              View all runs
            </Link>
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Started</TableHead>
                <TableHead>Counters</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {recentRuns.map((run) => (
                <TableRow key={run.id}>
                  <TableCell>{run.id}</TableCell>
                  <TableCell>{run.categorySlug ?? '—'}</TableCell>
                  <TableCell>
                    <StatusBadge status={run.status} />
                  </TableCell>
                  <TableCell>{formatDateTime(run.startedAt)}</TableCell>
                  <TableCell>
                    <RunCounters run={run} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  )
}
