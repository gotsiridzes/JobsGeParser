import { Link, useParams } from 'react-router-dom'
import { useUnsubscribeAll } from '@/api/alertHooks'
import { isNotFound } from '@/api/client'
import { AlertErrors } from '@/components/alerts/AlertErrors'
import { Button } from '@/components/ui/input'
import { useNoReferrer } from '@/lib/useNoReferrer'
import { AlertPage, InvalidLink } from './AlertPage'

/**
 * Asks before acting. Email link scanners open GET URLs automatically, so unsubscribing on load
 * would silently opt people out. The no-click path for mail clients is the API's RFC 8058 POST.
 */
export function AlertUnsubscribePage() {
  useNoReferrer()
  const { token = '' } = useParams()
  const unsubscribe = useUnsubscribeAll(token)

  if (unsubscribe.isError && isNotFound(unsubscribe.error)) return <InvalidLink />

  if (unsubscribe.isSuccess) {
    return (
      <AlertPage title="You are unsubscribed">
        <p className="text-sm text-muted-foreground">
          You will not receive further job alerts at this address.
        </p>
        <Link to="/alerts/new" className="text-sm text-primary hover:underline">
          Changed your mind? Create a new alert
        </Link>
      </AlertPage>
    )
  }

  return (
    <AlertPage title="Unsubscribe from job alerts">
      <p className="text-sm text-muted-foreground">
        This stops every job alert sent to this address. To stop just one, use{' '}
        <Link to={`/a/${token}`} className="text-primary hover:underline">
          manage alerts
        </Link>{' '}
        instead.
      </p>
      <AlertErrors error={unsubscribe.error} />
      <Button
        variant="destructive"
        disabled={unsubscribe.isPending}
        onClick={() => unsubscribe.mutate()}
      >
        {unsubscribe.isPending ? 'Unsubscribing…' : 'Unsubscribe from all alerts'}
      </Button>
    </AlertPage>
  )
}
