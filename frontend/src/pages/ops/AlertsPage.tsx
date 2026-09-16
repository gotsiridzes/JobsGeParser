import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useOpsAlerts } from '@/api/alertHooks'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Badge } from '@/components/ui/badge'
import { Button, Input, Select } from '@/components/ui/input'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { formatDateTime } from '@/lib/utils'

const STATUS_VARIANT: Record<string, 'success' | 'muted' | 'warning' | 'destructive'> = {
  Active: 'success',
  Paused: 'muted',
  PendingConfirmation: 'warning',
  Deleted: 'destructive',
}

const PAGE_SIZE = 25

export function AlertsPage() {
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState('')
  const [cadence, setCadence] = useState('')
  const [channel, setChannel] = useState('')
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')

  const { data, isLoading, isError, error, refetch } = useOpsAlerts({
    status: status || undefined,
    cadence: (cadence || undefined) as 'Instant' | 'DailyDigest' | undefined,
    channel: (channel || undefined) as 'Email' | 'Sms' | undefined,
    q: search || undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(searchInput)
    setPage(1)
  }

  function resetPage<T>(setter: (v: T) => void) {
    return (v: T) => {
      setter(v)
      setPage(1)
    }
  }

  const setStatusFiltered = resetPage(setStatus)
  const setCadenceFiltered = resetPage(setCadence)
  const setChannelFiltered = resetPage(setChannel)

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Alerts</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Every job alert subscription, across every subscriber
        </p>
      </div>

      <form onSubmit={handleSearch} className="flex flex-wrap gap-3 items-end">
        <div className="space-y-1 flex-1 min-w-[200px]">
          <label className="text-xs text-muted-foreground">Search</label>
          <Input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Alert name or address…"
          />
        </div>
        <div className="space-y-1">
          <label className="text-xs text-muted-foreground">Status</label>
          <Select
            value={status}
            onChange={(e) => setStatusFiltered(e.target.value)}
            className="w-40"
          >
            <option value="">All (except deleted)</option>
            <option value="Active">Active</option>
            <option value="Paused">Paused</option>
            <option value="PendingConfirmation">Pending confirmation</option>
            <option value="Deleted">Deleted</option>
          </Select>
        </div>
        <div className="space-y-1">
          <label className="text-xs text-muted-foreground">Cadence</label>
          <Select
            value={cadence}
            onChange={(e) => setCadenceFiltered(e.target.value)}
            className="w-36"
          >
            <option value="">All</option>
            <option value="Instant">Instant</option>
            <option value="DailyDigest">Daily digest</option>
          </Select>
        </div>
        <div className="space-y-1">
          <label className="text-xs text-muted-foreground">Channel</label>
          <Select
            value={channel}
            onChange={(e) => setChannelFiltered(e.target.value)}
            className="w-32"
          >
            <option value="">All</option>
            <option value="Email">Email</option>
            <option value="Sms">SMS</option>
          </Select>
        </div>
        <Button type="submit">Search</Button>
      </form>

      {isLoading && <LoadingState />}
      {isError && (
        <ErrorState
          message={error instanceof Error ? error.message : 'Failed to load alerts'}
          onRetry={() => refetch()}
        />
      )}

      {data && data.items.length === 0 && (
        <EmptyState message="No alert subscriptions match the current filters" />
      )}

      {data && data.items.length > 0 && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Address</TableHead>
                <TableHead>Channel</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Cadence</TableHead>
                <TableHead>Created</TableHead>
                <TableHead>Sent</TableHead>
                <TableHead>Last sent</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((sub) => (
                <TableRow key={sub.id}>
                  <TableCell>
                    <Link
                      to={`/ops/alerts/${sub.id}`}
                      className="font-medium text-primary hover:underline"
                    >
                      {sub.name}
                    </Link>
                  </TableCell>
                  <TableCell className="text-sm">{sub.address}</TableCell>
                  <TableCell>{sub.channel}</TableCell>
                  <TableCell>
                    <Badge variant={STATUS_VARIANT[sub.status] ?? 'muted'}>{sub.status}</Badge>
                  </TableCell>
                  <TableCell>{sub.cadence === 'DailyDigest' ? 'Daily digest' : 'Instant'}</TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {formatDateTime(sub.createdAt)}
                  </TableCell>
                  <TableCell>{sub.alertsSent}</TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {sub.lastAlertSentAt ? formatDateTime(sub.lastAlertSentAt) : '—'}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              {data.totalCount} alert{data.totalCount === 1 ? '' : 's'} · page {data.page} of{' '}
              {data.totalPages}
            </p>
            <div className="flex gap-2">
              <Button variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>
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
