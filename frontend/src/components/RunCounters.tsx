import type { ScrapeRun } from '@/api/types'

export function RunCounters({ run }: { run: ScrapeRun }) {
  return (
    <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground">
      <span title="Inserted">+{run.inserted}</span>
      <span title="Updated">~{run.updated}</span>
      <span title="Skipped">skip {run.skipped}</span>
      <span title="Failed" className={run.failed > 0 ? 'text-destructive' : undefined}>
        fail {run.failed}
      </span>
      <span title="Details fetched">det {run.detailsFetched}</span>
      <span title="Details skipped">det-skip {run.detailsSkipped}</span>
    </div>
  )
}
