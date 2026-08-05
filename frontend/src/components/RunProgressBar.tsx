import type { ScrapeRun } from '@/api/types'
import { cn } from '@/lib/utils'

function enrichPercent(run: ScrapeRun): number {
  if (run.status === 'Completed' || run.phase === 'Completed') return 100
  if (run.jobsNeedingDetails <= 0) return 100
  return Math.min(100, Math.round((100 * run.detailsFetched) / run.jobsNeedingDetails))
}

function progressLabel(run: ScrapeRun): string {
  if (run.phase === 'Discovering' || (run.status === 'Running' && run.phase !== 'Enriching')) {
    const parts = [`Discovering · page ${run.listingPagesFetched || 0}`]
    if (run.jobsDiscovered > 0) parts.push(`${run.jobsDiscovered} jobs`)
    return parts.join(' · ')
  }

  if (run.phase === 'Enriching' || run.status === 'Running') {
    if (run.jobsNeedingDetails <= 0) return 'Enriching · nothing to fetch'
    return `Enriching · ${run.detailsFetched} / ${run.jobsNeedingDetails} details`
  }

  if (run.status === 'Completed' || run.phase === 'Completed') {
    return `Done · ${run.detailsFetched} details`
  }

  return `Failed · ${run.detailsFetched} / ${run.jobsNeedingDetails || '?'} details`
}

export function RunProgressBar({ run, className }: { run: ScrapeRun; className?: string }) {
  const discovering =
    run.status === 'Running' &&
    (run.phase === 'Discovering' || (!run.phase && run.jobsNeedingDetails === 0 && run.detailsFetched === 0))
  const percent = discovering ? null : enrichPercent(run)

  return (
    <div className={cn('space-y-1 min-w-[10rem] max-w-xs', className)}>
      <div className="flex items-center justify-between gap-2 text-[11px] text-muted-foreground">
        <span className="truncate">{progressLabel(run)}</span>
        {percent != null && <span className="shrink-0 tabular-nums">{percent}%</span>}
      </div>
      <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
        {discovering ? (
          <div className="h-full w-1/3 rounded-full bg-primary/70 animate-pulse" />
        ) : (
          <div
            className={cn(
              'h-full rounded-full transition-[width] duration-300',
              run.status === 'Failed' ? 'bg-destructive' : 'bg-primary',
            )}
            style={{ width: `${percent ?? 0}%` }}
          />
        )}
      </div>
    </div>
  )
}
