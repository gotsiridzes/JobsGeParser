import type { AlertCriteria } from '@/api/alertTypes'
import { Badge } from '@/components/ui/badge'
import { formatLabel, formatSalary } from '@/lib/jobFormat'
import { cn } from '@/lib/utils'

/** Read-only chips describing a saved filter. */
export function AlertCriteriaSummary({
  criteria,
  className,
}: {
  criteria: AlertCriteria
  className?: string
}) {
  const chips: { key: string; label: string; variant?: 'secondary' | 'outline' }[] = []

  if (criteria.query) chips.push({ key: 'q', label: `"${criteria.query}"` })
  if (criteria.titleContains) chips.push({ key: 't', label: `title: ${criteria.titleContains}` })
  if (criteria.descriptionContains) chips.push({ key: 'd', label: `description: ${criteria.descriptionContains}` })
  if (criteria.companyContains) chips.push({ key: 'c', label: `company: ${criteria.companyContains}` })

  const lists: [string, string[] | null][] = [
    ['city', criteria.cities],
    ['mode', criteria.workModes],
    ['type', criteria.employmentTypes],
    ['level', criteria.seniorities],
    ['lang', criteria.languages],
  ]
  for (const [prefix, values] of lists) {
    for (const v of values ?? []) chips.push({ key: `${prefix}-${v}`, label: formatLabel(v) })
  }

  const salary = formatSalary(criteria.salaryMin, criteria.salaryMax, criteria.salaryCurrency, null)
  if (salary) chips.push({ key: 'salary', label: `salary ${salary}` })
  if (criteria.requireSalary) chips.push({ key: 'req', label: 'salary stated' })

  for (const slug of criteria.categorySlugs ?? []) {
    chips.push({ key: `cat-${slug}`, label: slug, variant: 'outline' })
  }

  if (chips.length === 0) return <p className={cn('text-sm text-muted-foreground', className)}>Any job</p>

  return (
    <div className={cn('flex flex-wrap gap-1.5', className)}>
      {chips.map((chip) => (
        <Badge key={chip.key} variant={chip.variant ?? 'secondary'} className="font-normal">
          {chip.label}
        </Badge>
      ))}
    </div>
  )
}
