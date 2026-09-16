import { useEffect, useRef } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useConfirmAlert } from '@/api/alertHooks'
import { isNotFound } from '@/api/client'
import { AlertErrors } from '@/components/alerts/AlertErrors'
import { LoadingState } from '@/components/StateViews'
import { useNoReferrer } from '@/lib/useNoReferrer'
import { AlertPage, InvalidLink } from './AlertPage'

export interface ConfirmedState {
  justConfirmed: true
  activated: number
}

export function AlertConfirmPage() {
  useNoReferrer()
  const { token = '' } = useParams()
  const navigate = useNavigate()
  const confirm = useConfirmAlert()

  // The confirmation token is single use, and StrictMode runs effects twice in development.
  // Without this guard the second call would 404 and show an error right after a success.
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true

    confirm.mutate(token, {
      onSuccess: (result) => {
        const state: ConfirmedState = { justConfirmed: true, activated: result.activatedSubscriptions }
        // Replace, so Back does not return to a link that no longer works.
        navigate(`/a/${result.manageToken}`, { replace: true, state })
      },
    })
    // Runs once per mount by design; the ref guards the StrictMode double invoke.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (confirm.isError) {
    if (isNotFound(confirm.error)) {
      return (
        <InvalidLink>
          This confirmation link is unknown or has expired.{' '}
          <Link to="/alerts/new" className="text-primary hover:underline">
            Create the alert again
          </Link>{' '}
          to get a new one.
        </InvalidLink>
      )
    }

    return (
      <AlertPage title="Could not confirm">
        <AlertErrors error={confirm.error} />
      </AlertPage>
    )
  }

  return (
    <AlertPage title="Confirming your alert">
      <LoadingState label="Confirming…" />
    </AlertPage>
  )
}
