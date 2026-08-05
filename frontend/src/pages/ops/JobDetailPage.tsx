import { Link, useParams } from 'react-router-dom'
import { useJob } from '@/api/hooks'
import { JobStructuredChips } from '@/components/JobStructuredChips'
import { ErrorState, LoadingState } from '@/components/StateViews'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { formatDateOnly, formatDateTime, toJobsGeUrl } from '@/lib/utils'

export function JobDetailPage() {
  const { id } = useParams()
  const jobId = Number(id)
  const { data, isLoading, isError, error, refetch } = useJob(jobId)

  if (isLoading) return <LoadingState />
  if (isError) {
    return (
      <ErrorState
        message={error instanceof Error ? error.message : 'Failed to load job'}
        onRetry={() => refetch()}
      />
    )
  }
  if (!data) return null

  return (
    <div className="space-y-6">
      <div>
        <Link to="/ops/jobs" className="text-sm text-primary hover:underline">
          ← Jobs
        </Link>
        <h1 className="text-2xl font-bold tracking-tight mt-2">{data.name}</h1>
        <p className="text-muted-foreground mt-1">{data.company}</p>
        <div className="mt-3">
          <JobStructuredChips
            salaryMin={data.salaryMin}
            salaryMax={data.salaryMax}
            salaryCurrency={data.salaryCurrency}
            salaryPeriod={data.salaryPeriod}
            city={data.city}
            workMode={data.workMode}
            employmentType={data.employmentType}
            seniority={data.seniority}
            languageRequirement={data.languageRequirement}
            categorySlugs={data.categorySlugs}
          />
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">ID</span>
              <span>{data.id}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Published</span>
              <span>{formatDateOnly(data.published)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">End date</span>
              <span>{formatDateOnly(data.endDate)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">First seen</span>
              <span>{formatDateTime(data.firstSeenAt)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Last seen</span>
              <span>{formatDateTime(data.lastSeenAt)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Details fetched</span>
              <span>{formatDateTime(data.detailsFetchedAt)}</span>
            </div>
            {data.updatedAt && (
              <div className="flex justify-between">
                <span className="text-muted-foreground">Updated</span>
                <span>{formatDateTime(data.updatedAt)}</span>
              </div>
            )}
            <div className="flex justify-between">
              <span className="text-muted-foreground">Enrichment</span>
              <span>v{data.enrichmentVersion}</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Categories & links</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex flex-wrap gap-1">
              {data.categorySlugs.map((slug) => (
                <Badge key={slug} variant="secondary">
                  {slug}
                </Badge>
              ))}
            </div>
            <a
              href={toJobsGeUrl(data.link)}
              target="_blank"
              rel="noopener noreferrer"
              className="text-sm text-primary hover:underline block"
            >
              View on jobs.ge →
            </a>
            {data.companyLink && (
              <a
                href={toJobsGeUrl(data.companyLink)}
                target="_blank"
                rel="noopener noreferrer"
                className="text-sm text-primary hover:underline block"
              >
                Company page →
              </a>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Description</CardTitle>
          <CardDescription>
            {data.description ? 'Full job description' : 'Description not yet fetched'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {data.description ? (
            <div className="whitespace-pre-wrap text-sm leading-relaxed">{data.description}</div>
          ) : (
            <p className="text-muted-foreground text-sm">No description available.</p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
