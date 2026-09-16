import { Link } from 'react-router-dom'

/** Shared frame for the pages reached from email links. */
export function AlertPage({
  title,
  children,
  wide,
}: {
  title: string
  children: React.ReactNode
  wide?: boolean
}) {
  return (
    <div className={`mx-auto space-y-6 px-4 py-8 ${wide ? 'max-w-3xl' : 'max-w-lg'}`}>
      <h1 className="font-display text-2xl tracking-tight">{title}</h1>
      {children}
    </div>
  )
}

/**
 * One message for every unknown, expired or used token, matching the backend, which answers
 * all of those with the same 404 so a caller cannot tell them apart.
 */
export function InvalidLink({ children }: { children?: React.ReactNode }) {
  return (
    <AlertPage title="Link not valid">
      <p className="text-sm text-muted-foreground">
        {children ?? 'This link is unknown or no longer works. Use the link from your most recent alert email.'}
      </p>
      <Link to="/" className="text-sm text-primary hover:underline">
        Go to job search
      </Link>
    </AlertPage>
  )
}
