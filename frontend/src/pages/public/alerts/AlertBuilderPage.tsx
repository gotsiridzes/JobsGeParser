import { FormEvent, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { BellRing, MailCheck } from 'lucide-react'
import { useAlertPreview, useAlertsAvailable, useCreateAlert } from '@/api/alertHooks'
import type { AlertCadence, AlertCriteria, CreateAlertResponse } from '@/api/alertTypes'
import { AlertCriteriaForm } from '@/components/alerts/AlertCriteriaForm'
import { AlertErrors } from '@/components/alerts/AlertErrors'
import { CadencePicker } from '@/components/alerts/CadencePicker'
import {
  browserTimeZone,
  cleanCriteria,
  criteriaFromSearchParams,
  defaultAlertName,
  isCriteriaEmpty,
  toTimeOnly,
} from '@/components/alerts/criteria'
import { JobResultCard } from '@/components/JobResultCard'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Field } from '@/components/ui/field'
import { Button, Input } from '@/components/ui/input'
import { useDebouncedValue } from '@/lib/useDebouncedValue'

export function AlertBuilderPage() {
  const [params] = useSearchParams()
  const available = useAlertsAvailable()

  const [criteria, setCriteria] = useState<AlertCriteria>(() =>
    criteriaFromSearchParams(params.get('q'), params.get('category')),
  )
  const [cadence, setCadence] = useState<AlertCadence>('Instant')
  const [sendTime, setSendTime] = useState('09:00')
  // Null means "not edited yet", so the suggestion keeps following the filter.
  const [name, setName] = useState<string | null>(null)
  const [email, setEmail] = useState('')
  const [submitted, setSubmitted] = useState<CreateAlertResponse | null>(null)

  const timeZone = useMemo(browserTimeZone, [])
  const cleaned = useMemo(() => cleanCriteria(criteria), [criteria])
  const debounced = useDebouncedValue(cleaned)
  const preview = useAlertPreview(debounced, available.isSuccess)
  const create = useCreateAlert()

  const empty = isCriteriaEmpty(cleaned)
  const effectiveName = name ?? defaultAlertName(cleaned)

  if (available.isLoading) return <Page><LoadingState /></Page>

  if (available.isError) {
    return (
      <Page>
        <EmptyState message="Job alerts are not available right now." />
      </Page>
    )
  }

  if (submitted) {
    return (
      <Page>
        <Card>
          <CardContent className="space-y-3 p-6 text-center">
            <MailCheck className="mx-auto h-10 w-10 text-primary" aria-hidden />
            <h1 className="font-display text-xl">Check your inbox</h1>
            <p className="text-sm text-muted-foreground">
              If <strong className="text-foreground">{email.trim()}</strong> can receive alerts, a
              confirmation link is on its way. Nothing is sent until you confirm.
            </p>
            <p className="text-sm">
              {submitted.currentlyMatchingJobs} jobs match this filter today. After you confirm,
              you will hear about new ones only.
            </p>
            <div className="flex justify-center gap-2 pt-2">
              <Link to="/search" className="text-sm text-primary hover:underline">
                Back to search
              </Link>
            </div>
          </CardContent>
        </Card>
      </Page>
    )
  }

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    create.mutate(
      {
        channel: 'Email',
        address: email.trim(),
        name: effectiveName.trim(),
        cadence,
        digestSendLocalTime: cadence === 'DailyDigest' ? toTimeOnly(sendTime) : null,
        timeZoneId: timeZone,
        criteria: cleaned,
      },
      { onSuccess: (response) => setSubmitted(response) },
    )
  }

  return (
    <Page>
      <div className="space-y-1">
        <h1 className="font-display text-2xl tracking-tight flex items-center gap-2">
          <BellRing className="h-6 w-6 text-primary" aria-hidden />
          New job alert
        </h1>
        <p className="text-sm text-muted-foreground">
          Describe the jobs you want and we will email you when a new one appears on jobs.ge.
        </p>
      </div>

      <form onSubmit={onSubmit} className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="space-y-6">
          <Card>
            <CardContent className="p-5">
              <AlertCriteriaForm value={criteria} onChange={setCriteria} options={available.data!} />
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-5 p-5">
              <CadencePicker
                cadence={cadence}
                sendTime={sendTime}
                timeZone={timeZone}
                onCadenceChange={setCadence}
                onSendTimeChange={setSendTime}
              />

              <div className="grid gap-3 sm:grid-cols-2">
                <Field label="Alert name" htmlFor="alert-name" hint="Shown in the emails you get.">
                  <Input
                    id="alert-name"
                    required
                    maxLength={100}
                    value={effectiveName}
                    onChange={(e) => setName(e.target.value)}
                  />
                </Field>
                <Field label="Email" htmlFor="alert-email" hint="We will send a confirmation link first.">
                  <Input
                    id="alert-email"
                    type="email"
                    required
                    autoComplete="email"
                    maxLength={320}
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                  />
                </Field>
              </div>

              <AlertErrors error={create.error} />

              <div className="flex flex-wrap items-center gap-3">
                <Button type="submit" disabled={empty || create.isPending}>
                  {create.isPending ? 'Creating…' : 'Create alert'}
                </Button>
                {empty && (
                  <span className="text-xs text-muted-foreground">Set at least one filter to continue.</span>
                )}
              </div>
            </CardContent>
          </Card>
        </div>

        <aside className="lg:sticky lg:top-20 lg:self-start">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Matching today</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <PreviewBody empty={empty} preview={preview} />
              <p className="text-xs text-muted-foreground">
                This is what matches right now. You will only be alerted about jobs posted after you
                confirm.
              </p>
            </CardContent>
          </Card>
        </aside>
      </form>
    </Page>
  )
}

function PreviewBody({
  empty,
  preview,
}: {
  empty: boolean
  preview: ReturnType<typeof useAlertPreview>
}) {
  if (empty) return <p className="text-sm text-muted-foreground">Set a filter to see matching jobs.</p>
  if (preview.isLoading) return <LoadingState label="Counting…" />
  if (preview.isError) {
    return (
      <ErrorState
        message={preview.error instanceof Error ? preview.error.message : 'Preview failed'}
        onRetry={() => preview.refetch()}
      />
    )
  }

  const data = preview.data
  if (!data) return null

  return (
    <div className={preview.isFetching ? 'opacity-60 transition-opacity' : undefined}>
      <p className="text-2xl font-semibold tabular-nums">{data.totalCount}</p>
      <p className="text-xs text-muted-foreground mb-2">
        {data.totalCount === 1 ? 'job matches' : 'jobs match'}
      </p>
      {data.items.map((job) => (
        <JobResultCard key={job.id} job={job} />
      ))}
    </div>
  )
}

function Page({ children }: { children: React.ReactNode }) {
  return <div className="mx-auto max-w-5xl space-y-6 px-4 py-8">{children}</div>
}
