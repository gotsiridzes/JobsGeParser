import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useScrapeRuns, useCategories } from '@/api/hooks'
import { RunCounters } from '@/components/RunCounters'
import { RunProgressBar } from '@/components/RunProgressBar'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Button, Input, Select } from '@/components/ui/input'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { formatDateTime, formatDuration } from '@/lib/utils'

const PAGE_SIZE = 25

export function RunsPage() {
  const [status, setStatus] = useState('')
  const [category, setCategory] = useState('')
  const [batchId, setBatchId] = useState('')
  const [offset, setOffset] = useState(0)

  const { data: categories } = useCategories()
  const { data, isLoading, isError, error, refetch } = useScrapeRuns({
    status: status || undefined,
    category: category || undefined,
    batchId: batchId || undefined,
    limit: PAGE_SIZE,
    offset,
  })

  const totalPages = data ? Math.ceil(data.totalCount / PAGE_SIZE) : 0
  const currentPage = Math.floor(offset / PAGE_SIZE) + 1

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Run History</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Paginated scrape run history with filters
        </p>
      </div>

      <div className="flex flex-wrap gap-3 items-end">
        <div className="space-y-1">
          <label className="text-xs text-muted-foreground">Status</label>
          <Select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value)
              setOffset(0)
            }}
            className="w-36"
          >
            <option value="">All</option>
            <option value="Running">Running</option>
            <option value="Completed">Completed</option>
            <option value="Failed">Failed</option>
          </Select>
        </div>
        <div className="space-y-1">
          <label className="text-xs text-muted-foreground">Category</label>
          <Select
            value={category}
            onChange={(e) => {
              setCategory(e.target.value)
              setOffset(0)
            }}
            className="w-40"
          >
            <option value="">All</option>
            {categories?.map((c) => (
              <option key={c.slug} value={c.slug}>
                {c.slug}
              </option>
            ))}
          </Select>
        </div>
        <div className="space-y-1">
          <label className="text-xs text-muted-foreground">Batch ID</label>
          <Input
            value={batchId}
            onChange={(e) => {
              setBatchId(e.target.value)
              setOffset(0)
            }}
            placeholder="uuid…"
            className="w-64 font-mono text-xs"
          />
        </div>
      </div>

      {isLoading && <LoadingState />}
      {isError && (
        <ErrorState
          message={error instanceof Error ? error.message : 'Failed to load runs'}
          onRetry={() => refetch()}
        />
      )}

      {data && data.items.length === 0 && (
        <EmptyState message="No runs match the current filters" />
      )}

      {data && data.items.length > 0 && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Started</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead>Batch</TableHead>
                <TableHead>Progress</TableHead>
                <TableHead>Counters</TableHead>
                <TableHead>Error</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((run) => (
                <TableRow key={run.id}>
                  <TableCell>{run.id}</TableCell>
                  <TableCell className="font-medium">{run.categorySlug ?? '—'}</TableCell>
                  <TableCell>
                    <StatusBadge status={run.status} />
                  </TableCell>
                  <TableCell>{formatDateTime(run.startedAt)}</TableCell>
                  <TableCell>{formatDuration(run.startedAt, run.finishedAt)}</TableCell>
                  <TableCell>
                    {run.batchId ? (
                      <Link
                        to={`/ops/batches/${run.batchId}`}
                        className="text-primary hover:underline font-mono text-xs"
                      >
                        {run.batchId.slice(0, 8)}…
                      </Link>
                    ) : (
                      '—'
                    )}
                  </TableCell>
                  <TableCell>
                    <RunProgressBar run={run} />
                  </TableCell>
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

          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              {data.totalCount} runs · page {currentPage} of {totalPages || 1}
            </p>
            <div className="flex gap-2">
              <Button
                variant="outline"
                disabled={offset === 0}
                onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                disabled={offset + PAGE_SIZE >= data.totalCount}
                onClick={() => setOffset(offset + PAGE_SIZE)}
              >
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
