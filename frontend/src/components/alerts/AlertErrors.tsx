import { ApiError } from '@/api/client'

/** Renders a mutation error, including every field message the backend returned. */
export function AlertErrors({ error }: { error: unknown }) {
  if (!error) return null

  const messages =
    error instanceof ApiError && error.errors.length > 0
      ? error.errors
      : [error instanceof Error ? error.message : 'Something went wrong']

  return (
    <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
      {messages.length === 1 ? (
        <p>{messages[0]}</p>
      ) : (
        <ul className="list-disc pl-5 space-y-0.5">
          {messages.map((m) => (
            <li key={m}>{m}</li>
          ))}
        </ul>
      )}
    </div>
  )
}
