import type { AlertCadence } from '@/api/alertTypes'
import { Input } from '@/components/ui/input'
import { FieldGroup } from '@/components/ui/field'
import { cn } from '@/lib/utils'

const CHOICES: { value: AlertCadence; title: string; description: string }[] = [
  { value: 'Instant', title: 'As soon as it is posted', description: 'One email per matching job.' },
  { value: 'DailyDigest', title: 'Daily digest', description: 'One email a day listing every new match.' },
]

export function CadencePicker({
  cadence,
  sendTime,
  timeZone,
  onCadenceChange,
  onSendTimeChange,
  idPrefix = 'alert',
  disabled,
}: {
  cadence: AlertCadence
  /** HH:mm */
  sendTime: string
  timeZone?: string | null
  onCadenceChange: (cadence: AlertCadence) => void
  onSendTimeChange: (hhmm: string) => void
  idPrefix?: string
  disabled?: boolean
}) {
  return (
    <FieldGroup legend="How often">
      <div className="grid gap-2 sm:grid-cols-2">
        {CHOICES.map((choice) => {
          const checked = cadence === choice.value
          return (
            <label
              key={choice.value}
              className={cn(
                'flex cursor-pointer gap-3 rounded-md border p-3 transition-colors',
                checked ? 'border-primary bg-primary/5' : 'border-input hover:bg-accent',
                disabled && 'pointer-events-none opacity-50',
              )}
            >
              <input
                type="radio"
                name={`${idPrefix}-cadence`}
                value={choice.value}
                checked={checked}
                disabled={disabled}
                onChange={() => onCadenceChange(choice.value)}
                className="mt-0.5 accent-[hsl(var(--primary))]"
              />
              <span>
                <span className="block text-sm font-medium">{choice.title}</span>
                <span className="block text-xs text-muted-foreground">{choice.description}</span>
              </span>
            </label>
          )
        })}
      </div>

      {cadence === 'DailyDigest' && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <label htmlFor={`${idPrefix}-send-time`}>Send at</label>
          <Input
            id={`${idPrefix}-send-time`}
            type="time"
            className="w-32"
            value={sendTime}
            disabled={disabled}
            onChange={(e) => onSendTimeChange(e.target.value)}
          />
          {timeZone && <span className="text-muted-foreground">your time ({timeZone})</span>}
        </div>
      )}
    </FieldGroup>
  )
}
