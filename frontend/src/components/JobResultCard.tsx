import { Link } from 'react-router-dom'
import { ExternalLink } from 'lucide-react'
import { JobStructuredChips } from '@/components/JobStructuredChips'
import type { JobListItem, SearchResult } from '@/api/types'
import { formatDateOnly } from '@/lib/utils'

type JobCardSource = Pick<
  JobListItem | SearchResult,
  | 'id'
  | 'name'
  | 'link'
  | 'company'
  | 'published'
  | 'endDate'
  | 'salaryMin'
  | 'salaryMax'
  | 'salaryCurrency'
  | 'salaryPeriod'
  | 'city'
  | 'workMode'
  | 'employmentType'
  | 'seniority'
  | 'languageRequirement'
> & { rank?: number | null }

type Props = {
  job: JobCardSource
  searchState?: { q?: string; category?: string; page?: number }
}

export function JobResultCard({ job, searchState }: Props) {
  return (
    <article className="border-b border-border/70 py-4 first:pt-0 last:border-b-0">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1.5">
          <Link
            to={`/jobs/${job.id}`}
            state={searchState}
            className="font-medium text-foreground hover:text-[hsl(var(--public-accent))] transition-colors"
          >
            {job.name}
          </Link>
          <p className="text-sm text-muted-foreground">{job.company}</p>
          <JobStructuredChips {...job} />
          <p className="text-xs text-muted-foreground">
            Posted {formatDateOnly(job.published)}
            {job.endDate ? ` · Ends ${formatDateOnly(job.endDate)}` : ''}
            {job.rank != null ? ` · rank ${job.rank.toFixed(3)}` : ''}
          </p>
        </div>
        <a
          href={job.link}
          target="_blank"
          rel="noopener noreferrer"
          className="shrink-0 inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          onClick={(e) => e.stopPropagation()}
        >
          jobs.ge
          <ExternalLink className="h-3 w-3" />
        </a>
      </div>
    </article>
  )
}
