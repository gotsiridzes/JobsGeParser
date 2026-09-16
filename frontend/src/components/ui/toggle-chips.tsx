import { Check } from 'lucide-react'
import { cn } from '@/lib/utils'

/**
 * Multi-select pill group. Suited to the small, closed vocabularies the enrichment step
 * produces. Selection order follows the option order, not click order.
 */
export function ToggleChips({
  options,
  value,
  onChange,
  formatOption = (option) => option,
  label,
  disabled,
  className,
}: {
  options: readonly string[]
  value: readonly string[]
  onChange: (next: string[]) => void
  formatOption?: (option: string) => string
  /** Accessible name for the group. */
  label: string
  disabled?: boolean
  className?: string
}) {
  const selected = new Set(value.map((v) => v.toLowerCase()))

  const toggle = (option: string) => {
    const key = option.toLowerCase()
    const next = selected.has(key)
      ? options.filter((o) => selected.has(o.toLowerCase()) && o.toLowerCase() !== key)
      : options.filter((o) => selected.has(o.toLowerCase()) || o.toLowerCase() === key)
    onChange(next)
  }

  return (
    <div role="group" aria-label={label} className={cn('flex flex-wrap gap-1.5', className)}>
      {options.map((option) => {
        const on = selected.has(option.toLowerCase())
        return (
          <button
            key={option}
            type="button"
            aria-pressed={on}
            disabled={disabled}
            onClick={() => toggle(option)}
            className={cn(
              'inline-flex items-center gap-1 rounded-full border px-3 py-1 text-xs font-medium transition-colors',
              'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring',
              'disabled:pointer-events-none disabled:opacity-50',
              on
                ? 'border-primary bg-primary text-primary-foreground'
                : 'border-input bg-background text-foreground hover:bg-accent',
            )}
          >
            {on && <Check className="h-3 w-3" aria-hidden />}
            {formatOption(option)}
          </button>
        )
      })}
    </div>
  )
}
