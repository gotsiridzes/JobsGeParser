import { Badge } from '@/components/ui/badge'
import type { JobStructuredFields } from '@/api/types'
import { formatSalary, formatLabel } from '@/lib/jobFormat'

type Props = JobStructuredFields & {
  categorySlugs?: string[]
  className?: string
}

export function JobStructuredChips({
  salaryMin,
  salaryMax,
  salaryCurrency,
  salaryPeriod,
  city,
  workMode,
  employmentType,
  seniority,
  languageRequirement,
  categorySlugs,
  className,
}: Props) {
  const salary = formatSalary(salaryMin, salaryMax, salaryCurrency, salaryPeriod)
  const chips: { key: string; label: string; variant?: 'secondary' | 'outline' }[] = []

  if (salary) chips.push({ key: 'salary', label: salary })
  if (city) chips.push({ key: 'city', label: city })
  if (workMode) chips.push({ key: 'workMode', label: formatLabel(workMode) })
  if (employmentType) chips.push({ key: 'employmentType', label: formatLabel(employmentType) })
  if (seniority) chips.push({ key: 'seniority', label: formatLabel(seniority) })
  if (languageRequirement)
    chips.push({ key: 'lang', label: languageRequirement.toUpperCase() })
  for (const slug of categorySlugs ?? []) {
    chips.push({ key: `cat-${slug}`, label: slug, variant: 'outline' })
  }

  if (chips.length === 0) return null

  return (
    <div className={`flex flex-wrap gap-1.5 ${className ?? ''}`}>
      {chips.map((chip) => (
        <Badge key={chip.key} variant={chip.variant ?? 'secondary'}>
          {chip.label}
        </Badge>
      ))}
    </div>
  )
}
