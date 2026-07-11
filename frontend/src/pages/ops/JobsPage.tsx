import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useJobs, useCategories } from '@/api/hooks'
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
import { formatDateOnly, formatDateTime } from '@/lib/utils'

export function JobsPage() {
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [category, setCategory] = useState('')
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')

  const { data: categories } = useCategories()
  const { data, isLoading, isError, error, refetch } = useJobs({
    page,
    pageSize,
    category: category || undefined,
    q: search || undefined,
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(searchInput)
    setPage(1)
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Jobs</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Browse scraped job listings
        </p>
      </div>

      <form onSubmit={handleSearch} className="flex flex-wrap gap-3 items-end">
        <div className="space-y-1 flex-1 min-w-[200px]">
          <label className="text-xs text-muted-foreground">Search</label>
          <Input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Title or company…"
          />
        </div>
        <div className="space-y-1">
          <label className="text-xs text-muted-foreground">Category</label>
          <Select
            value={category}
            onChange={(e) => {
              setCategory(e.target.value)
              setPage(1)
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
        <Button type="submit">Search</Button>
      </form>

      {isLoading && <LoadingState />}
      {isError && (
        <ErrorState
          message={error instanceof Error ? error.message : 'Failed to load jobs'}
          onRetry={() => refetch()}
        />
      )}

      {data && data.items.length === 0 && (
        <EmptyState message="No jobs match the current filters" />
      )}

      {data && data.items.length > 0 && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Title</TableHead>
                <TableHead>Company</TableHead>
                <TableHead>Published</TableHead>
                <TableHead>End date</TableHead>
                <TableHead>Last seen</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((job) => (
                <TableRow key={job.id}>
                  <TableCell>{job.id}</TableCell>
                  <TableCell>
                    <Link
                      to={`/ops/jobs/${job.id}`}
                      className="font-medium text-primary hover:underline"
                    >
                      {job.name}
                    </Link>
                  </TableCell>
                  <TableCell>{job.company}</TableCell>
                  <TableCell>{formatDateOnly(job.published)}</TableCell>
                  <TableCell>{formatDateOnly(job.endDate)}</TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {formatDateTime(job.lastSeenAt)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              {data.totalCount} jobs · page {data.page} of {data.totalPages}
            </p>
            <div className="flex gap-2">
              <Button
                variant="outline"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                disabled={page >= data.totalPages}
                onClick={() => setPage(page + 1)}
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
