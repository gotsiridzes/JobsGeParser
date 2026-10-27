import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Pause, Pencil, Play, Trash2 } from 'lucide-react'
import {
  useAlertsAvailable,
  useOpsAlert,
  useOpsDeleteAlert,
  useOpsPauseAlert,
  useOpsResumeAlert,
  useOpsUpdateAlert,
} from '@/api/alertHooks'
import type { AlertCadence, AlertCriteria } from '@/api/alertTypes'
import { isNotFound } from '@/api/client'
import { AlertCriteriaForm } from '@/components/alerts/AlertCriteriaForm'
import { AlertCriteriaSummary } from '@/components/alerts/AlertCriteriaSummary'
import { AlertErrors } from '@/components/alerts/AlertErrors'
import { CadencePicker } from '@/components/alerts/CadencePicker'
import { cleanCriteria, fromTimeOnly, isCriteriaEmpty, toTimeOnly } from '@/components/alerts/criteria'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Badge, type BadgeProps } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Field } from '@/components/ui/field'
import { Button, Input } from '@/components/ui/input'
import { formatDateTime } from '@/lib/utils'

const STATUS_BADGE: Record<string, { label: string; variant: BadgeProps['variant'] }> = {
  Active: { label: 'Active', variant: 'success' },
  Paused: { label: 'Paused', variant: 'muted' },
  PendingConfirmation: { label: 'Waiting for confirmation', variant: 'warning' },
  Deleted: { label: 'Deleted', variant: 'destructive' },
}

export function AlertDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const [editing, setEditing] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const { data, isLoading, isError, error, refetch } = useOpsAlert(id)
  const pause = useOpsPauseAlert(id)
  const resume = useOpsResumeAlert(id)
  const remove = useOpsDeleteAlert(id)

  if (isLoading) return <LoadingState />

  if (isError) {
    if (isNotFound(error)) return <EmptyState message="This alert subscription does not exist." />
    return (
      <ErrorState
        message={error instanceof Error ? error.message : 'Failed to load alert'}
        onRetry={() => refetch()}
      />
    )
  }

  if (!data) return null

  const status = STATUS_BADGE[data.status] ?? { label: data.status, variant: 'muted' as const }
  const busy = pause.isPending || resume.isPending || remove.isPending
  const actionError = pause.error ?? resume.error ?? remove.error

  const onDeleteConfirmed = () =>
    remove.mutate(undefined, { onSuccess: () => navigate('/ops/alerts') })

  return (
    <div className="space-y-6">
      <div>
        <Link to="/ops/alerts" className="text-sm text-primary hover:underline">
          ← Alerts
        </Link>
        <div className="flex flex-wrap items-start justify-between gap-2 mt-2">
          <h1 className="text-2xl font-bold tracking-tight">{data.name}</h1>
          <Badge variant={status.variant}>{status.label}</Badge>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Subscriber</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Address</span>
              <span>{data.address}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Channel</span>
              <span>{data.channel}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Subscriber status</span>
              <span>{data.subscriberStatus}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Subscriber ID</span>
              <span className="font-mono text-xs">{data.subscriberId}</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Delivery</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Cadence</span>
              <span>
                {data.cadence === 'DailyDigest'
                  ? `Daily digest at ${fromTimeOnly(data.digestSendLocalTime)}`
                  : 'Instant'}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Created</span>
              <span>{formatDateTime(data.createdAt)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Matches jobs from</span>
              <span>{formatDateTime(data.matchFromAt)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Alerts sent</span>
              <span>{data.alertsSent}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Last sent</span>
              <span>{data.lastAlertSentAt ? formatDateTime(data.lastAlertSentAt) : '—'}</span>
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Filter</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {editing ? (
            <AlertEditor
              id={id}
              subscription={data}
              onDone={() => setEditing(false)}
            />
          ) : (
            <>
              <AlertCriteriaSummary criteria={data.criteria} />
              <AlertErrors error={actionError} />
              <div className="flex flex-wrap gap-2">
                {data.status === 'Active' && (
                  <Button size="sm" variant="outline" disabled={busy} onClick={() => pause.mutate()}>
                    <Pause className="h-3.5 w-3.5" aria-hidden />
                    Pause
                  </Button>
                )}
                {data.status === 'Paused' && (
                  <Button size="sm" variant="outline" disabled={busy} onClick={() => resume.mutate()}>
                    <Play className="h-3.5 w-3.5" aria-hidden />
                    Resume
                  </Button>
                )}
                <Button size="sm" variant="outline" disabled={busy} onClick={() => setEditing(true)}>
                  <Pencil className="h-3.5 w-3.5" aria-hidden />
                  Edit
                </Button>
                {confirmDelete ? (
                  <>
                    <Button size="sm" variant="destructive" disabled={busy} onClick={onDeleteConfirmed}>
                      Delete this alert
                    </Button>
                    <Button size="sm" variant="ghost" disabled={busy} onClick={() => setConfirmDelete(false)}>
                      Keep it
                    </Button>
                  </>
                ) : (
                  <Button size="sm" variant="ghost" disabled={busy} onClick={() => setConfirmDelete(true)}>
                    <Trash2 className="h-3.5 w-3.5" aria-hidden />
                    Delete
                  </Button>
                )}
              </div>
              {data.status === 'Paused' && (
                <p className="text-xs text-muted-foreground">
                  Resuming starts from now; jobs posted while paused are not sent.
                </p>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function AlertEditor({
  id,
  subscription,
  onDone,
}: {
  id: string
  subscription: { name: string; cadence: AlertCadence; digestSendLocalTime: string; criteria: AlertCriteria }
  onDone: () => void
}) {
  const available = useAlertsAvailable()
  const update = useOpsUpdateAlert(id)

  const [criteria, setCriteria] = useState<AlertCriteria>(subscription.criteria)
  const [cadence, setCadence] = useState<AlertCadence>(subscription.cadence)
  const [sendTime, setSendTime] = useState(fromTimeOnly(subscription.digestSendLocalTime))
  const [name, setName] = useState(subscription.name)

  if (available.isLoading) return <LoadingState />
  if (available.isError) return <ErrorState message="Alert settings are unavailable right now." />

  const cleaned = cleanCriteria(criteria)
  const idPrefix = `ops-edit-${id}`

  const save = () =>
    update.mutate(
      {
        name: name.trim(),
        cadence,
        digestSendLocalTime: toTimeOnly(sendTime),
        criteria: cleaned,
      },
      { onSuccess: onDone },
    )

  return (
    <div className="space-y-5">
      <Field label="Alert name" htmlFor={`${idPrefix}-name`}>
        <Input
          id={`${idPrefix}-name`}
          maxLength={100}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </Field>

      <AlertCriteriaForm
        value={criteria}
        onChange={setCriteria}
        options={available.data!}
        idPrefix={idPrefix}
        disabled={update.isPending}
      />

      <CadencePicker
        cadence={cadence}
        sendTime={sendTime}
        onCadenceChange={setCadence}
        onSendTimeChange={setSendTime}
        idPrefix={idPrefix}
        disabled={update.isPending}
      />

      {/* Includes the 409 when this filter duplicates another alert for the same subscriber. */}
      <AlertErrors error={update.error} />

      <div className="flex gap-2">
        <Button
          size="sm"
          disabled={update.isPending || isCriteriaEmpty(cleaned) || name.trim() === ''}
          onClick={save}
        >
          {update.isPending ? 'Saving…' : 'Save changes'}
        </Button>
        <Button size="sm" variant="ghost" disabled={update.isPending} onClick={onDone}>
          Cancel
        </Button>
      </div>
    </div>
  )
}
