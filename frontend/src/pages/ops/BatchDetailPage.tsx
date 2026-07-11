import { Link, useParams } from 'react-router-dom'
import { useBatch } from '@/api/hooks'
import { RunCounters } from '@/components/RunCounters'
import { StatusBadge } from '@/components/StatusBadge'
import { ErrorState, LoadingState } from '@/components/StateViews'
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

export function BatchDetailPage() {
  const { batchId = '' } = useParams()
  const { data, isLoading, isError, error, refetch } = useBatch(batchId)

  if (isLoading) return <LoadingState />
  if (isError) {
    return (
      <ErrorState
        message={error instanceof Error ? error.message : 'Failed to load batch'}
        onRetry={() => refetch()}
      />
    )
  }
  if (!data) return null

  return (
    <div className="space-y-6">
      <div>
        <Link to="/ops" className="text-sm text-primary hover:underline">
          ← Dashboard
        </Link>
        <h1 className="text-2xl font-bold tracking-tight mt-2">Batch detail</h1>
        <p className="font-mono text-xs text-muted-foreground mt-1">{data.batchId}</p>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Started</CardDescription>
            <CardTitle className="text-lg">{formatDateTime(data.startedAt)}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Finished</CardDescription>
            <CardTitle className="text-lg">{formatDateTime(data.finishedAt)}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Total runs</CardDescription>
            <CardTitle className="text-2xl">{data.totalRuns}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Status breakdown</CardDescription>
            <CardTitle className="text-base flex flex-wrap gap-1">
              {data.running > 0 && <Badge variant="warning">{data.running} running</Badge>}
              <Badge variant="success">{data.completed} done</Badge>
              {data.failed > 0 && <Badge variant="destructive">{data.failed} failed</Badge>}
            </CardTitle>
          </CardHeader>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Category runs</CardTitle>
          <CardDescription>All scrape runs in this tick</CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Started</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead>Counters</TableHead>
                <TableHead>Error</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.runs.map((run) => (
                <TableRow key={run.id}>
                  <TableCell>{run.id}</TableCell>
                  <TableCell className="font-medium">{run.categorySlug ?? '—'}</TableCell>
                  <TableCell>
                    <StatusBadge status={run.status} />
                  </TableCell>
                  <TableCell>{formatDateTime(run.startedAt)}</TableCell>
                  <TableCell>{formatDuration(run.startedAt, run.finishedAt)}</TableCell>
                  <TableCell>
                    <RunCounters run={run} />
                  </TableCell>
                  <TableCell className="max-w-xs truncate text-destructive text-xs">
                    {run.errorMessage ?? '—'}
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
