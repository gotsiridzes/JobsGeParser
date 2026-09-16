import { cn } from '@/lib/utils'

export function Label({ className, ...props }: React.LabelHTMLAttributes<HTMLLabelElement>) {
  return <label className={cn('text-sm font-medium leading-none', className)} {...props} />
}

/** Label, control, optional hint and error, stacked. */
export function Field({
  label,
  htmlFor,
  hint,
  error,
  className,
  children,
}: {
  label: string
  htmlFor?: string
  hint?: React.ReactNode
  error?: string | null
  className?: string
  children: React.ReactNode
}) {
  return (
    <div className={cn('space-y-1.5', className)}>
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint && !error && <p className="text-xs text-muted-foreground">{hint}</p>}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  )
}

/** A titled group of controls; renders as a fieldset so screen readers announce the legend. */
export function FieldGroup({
  legend,
  description,
  className,
  children,
}: {
  legend: string
  description?: React.ReactNode
  className?: string
  children: React.ReactNode
}) {
  return (
    <fieldset className={cn('space-y-3', className)}>
      <div>
        <legend className="text-sm font-semibold">{legend}</legend>
        {description && <p className="text-xs text-muted-foreground mt-0.5">{description}</p>}
      </div>
      {children}
    </fieldset>
  )
}
