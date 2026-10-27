import { useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { CheckCircle2, Pause, Pencil, Play, Plus, Trash2 } from 'lucide-react'
import {
  useAlertsAvailable,
  useDeleteAlert,
  useManagedAlerts,
  usePauseAlert,
  useResumeAlert,
  useUnsubscribeAll,
  useUpdateAlert,
} from '@/api/alertHooks'
import type { AlertCadence, AlertCriteria, AlertSubscription } from '@/api/alertTypes'
import { isNotFound } from '@/api/client'
import { AlertCriteriaForm } from '@/components/alerts/AlertCriteriaForm'
import { AlertCriteriaSummary } from '@/components/alerts/AlertCriteriaSummary'
import { AlertErrors } from '@/components/alerts/AlertErrors'
import { CadencePicker } from '@/components/alerts/CadencePicker'
import { cleanCriteria, fromTimeOnly, isCriteriaEmpty, toTimeOnly } from '@/components/alerts/criteria'
import { EmptyState, ErrorState, LoadingState } from '@/components/StateViews'
import { Badge, type BadgeProps } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Field } from '@/components/ui/field'
import { Button, Input } from '@/components/ui/input'
import { formatDateTime } from '@/lib/utils'
import { useNoReferrer } from '@/lib/useNoReferrer'
import { AlertPage, InvalidLink } from './AlertPage'
import type { ConfirmedState } from './AlertConfirmPage'

const STATUS_BADGE: Record<string, { label: string; variant: BadgeProps['variant'] }> = {
  Active: { label: 'Active', variant: 'success' },
  Paused: { label: 'Paused', variant: 'muted' },
  PendingConfirmation: { label: 'Waiting for confirmation', variant: 'warning' },
}

export function AlertManagePage() {
  useNoReferrer()
  const { token = '' } = useParams()
  const location = useLocation()
  const confirmed = (location.state as ConfirmedState | null)?.justConfirmed
    ? (location.state as ConfirmedState)
    : null

  const managed = useManagedAlerts(token)
  const unsubscribeAll = useUnsubscribeAll(token)

  if (managed.isLoading) {
    return (
      <AlertPage title="Your job alerts" wide>
        <LoadingState />
      </AlertPage>
    )
  }

  if (managed.isError) {
    if (isNotFound(managed.error)) return <InvalidLink />
    return (
      <AlertPage title="Your job alerts" wide>
        <ErrorState
          message={managed.error instanceof Error ? managed.error.message : 'Could not load alerts'}
          onRetry={() => managed.refetch()}
        />
      </AlertPage>
    )
  }

  const data = managed.data!
  const unsubscribed = data.subscriberStatus === 'Unsubscribed'

  return (
    <AlertPage title="Your job alerts" wide>
      {confirmed && (
        <div role="status" className="flex items-start gap-2 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          <p>
            Confirmed. {confirmed.activated === 1 ? '1 alert is' : `${confirmed.activated} alerts are`} now
            active. You will hear about new matching jobs from now on.
          </p>
        </div>
      )}

      <div className="space-y-1 text-sm">
        <p>
          Sent to <strong>{data.maskedAddress}</strong>
          {unsubscribed && <Badge variant="muted" className="ml-2">Unsubscribed</Badge>}
        </p>
        <p className="text-xs text-muted-foreground">
          Anyone with this page's link can manage these alerts. You can find it again at the bottom of
          any alert email.
        </p>
      </div>

      {data.subscriptions.length === 0 ? (
        <EmptyState
          message={unsubscribed ? 'You are unsubscribed from all alerts.' : 'You have no alerts yet.'}
        />
      ) : (
        <div className="space-y-3">
          {data.subscriptions.map((subscription) => (
            <SubscriptionCard key={subscription.id} token={token} subscription={subscription} />
          ))}
        </div>
      )}

      <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4">
        <Link
          to="/alerts/new"
          className="inline-flex items-center gap-1.5 text-sm text-primary hover:underline"
        >
          <Plus className="h-4 w-4" aria-hidden />
          Create another alert
        </Link>

        {!unsubscribed && (
          <UnsubscribeAllButton
            pending={unsubscribeAll.isPending}
            error={unsubscribeAll.error}
            onConfirm={() => unsubscribeAll.mutate()}
          />
        )}
      </div>
    </AlertPage>
  )
}

function SubscriptionCard({ token, subscription }: { token: string; subscription: AlertSubscription }) {
  const [editing, setEditing] = useState(false)
  const pause = usePauseAlert(token)
  const resume = useResumeAlert(token)
  const remove = useDeleteAlert(token)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const status = STATUS_BADGE[subscription.status] ?? { label: subscription.status, variant: 'muted' as const }
  const busy = pause.isPending || resume.isPending || remove.isPending
  const actionError = pause.error ?? resume.error ?? remove.error

  return (
    <Card>
      <CardContent className="space-y-3 p-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="space-y-1">
            <h2 className="font-medium">{subscription.name}</h2>
            <p className="text-xs text-muted-foreground">
              {subscription.cadence === 'DailyDigest'
                ? `Daily digest at ${fromTimeOnly(subscription.digestSendLocalTime)}`
                : 'As soon as a job is posted'}
              {' · '}
              {subscription.alertsSent} sent
              {subscription.lastAlertSentAt && ` · last ${formatDateTime(subscription.lastAlertSentAt)}`}
            </p>
          </div>
          <Badge variant={status.variant}>{status.label}</Badge>
        </div>

        {editing ? (
          <SubscriptionEditor token={token} subscription={subscription} onDone={() => setEditing(false)} />
        ) : (
          <>
            <AlertCriteriaSummary criteria={subscription.criteria} />

            <AlertErrors error={actionError} />

            <div className="flex flex-wrap gap-2">
              {subscription.status === 'Active' && (
                <Button size="sm" variant="outline" disabled={busy} onClick={() => pause.mutate(subscription.id)}>
                  <Pause className="h-3.5 w-3.5" aria-hidden />
                  Pause
                </Button>
              )}
              {subscription.status === 'Paused' && (
                <Button size="sm" variant="outline" disabled={busy} onClick={() => resume.mutate(subscription.id)}>
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
                  <Button size="sm" variant="destructive" disabled={busy} onClick={() => remove.mutate(subscription.id)}>
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
            {subscription.status === 'Paused' && (
              <p className="text-xs text-muted-foreground">
                Resuming starts from now; jobs posted while paused are not sent.
              </p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  )
}

function SubscriptionEditor({
  token,
  subscription,
  onDone,
}: {
  token: string
  subscription: AlertSubscription
  onDone: () => void
}) {
  const available = useAlertsAvailable()
  const update = useUpdateAlert(token)

  const [criteria, setCriteria] = useState<AlertCriteria>(subscription.criteria)
  const [cadence, setCadence] = useState<AlertCadence>(subscription.cadence)
  const [sendTime, setSendTime] = useState(fromTimeOnly(subscription.digestSendLocalTime))
  const [name, setName] = useState(subscription.name)

  if (available.isLoading) return <LoadingState />
  if (available.isError) return <ErrorState message="Alert settings are unavailable right now." />

  const cleaned = cleanCriteria(criteria)
  const idPrefix = `edit-${subscription.id}`

  const save = () =>
    update.mutate(
      {
        id: subscription.id,
        patch: {
          name: name.trim(),
          cadence,
          digestSendLocalTime: toTimeOnly(sendTime),
          criteria: cleaned,
        },
      },
      { onSuccess: onDone },
    )

  return (
    <div className="space-y-5 border-t pt-4">
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

      {/* Includes the 409 when this filter duplicates another of your alerts. */}
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

function UnsubscribeAllButton({
  pending,
  error,
  onConfirm,
}: {
  pending: boolean
  error: unknown
  onConfirm: () => void
}) {
  const [armed, setArmed] = useState(false)

  return (
    <div className="flex flex-col items-end gap-2">
      <AlertErrors error={error} />
      {armed ? (
        <div className="flex gap-2">
          <Button size="sm" variant="destructive" disabled={pending} onClick={onConfirm}>
            {pending ? 'Unsubscribing…' : 'Yes, stop all alerts'}
          </Button>
          <Button size="sm" variant="ghost" disabled={pending} onClick={() => setArmed(false)}>
            Cancel
          </Button>
        </div>
      ) : (
        <Button size="sm" variant="ghost" onClick={() => setArmed(true)}>
          Unsubscribe from everything
        </Button>
      )}
    </div>
  )
}
