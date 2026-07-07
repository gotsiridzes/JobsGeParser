import { Badge } from '@/components/ui/badge'
import type { ScrapeRunStatus } from '@/api/types'

const statusVariant: Record<
  ScrapeRunStatus,
  'warning' | 'success' | 'destructive' | 'muted'
> = {
  Running: 'warning',
  Completed: 'success',
  Failed: 'destructive',
}

export function StatusBadge({ status }: { status: ScrapeRunStatus | string }) {
  const variant = statusVariant[status as ScrapeRunStatus] ?? 'muted'
  return <Badge variant={variant}>{status}</Badge>
}
