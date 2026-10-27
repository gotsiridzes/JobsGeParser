import { useMemo } from 'react'
import { useCategories } from '@/api/hooks'
import type { AlertCriteria, AlertCriteriaOptions } from '@/api/alertTypes'
import { Field, FieldGroup, Label } from '@/components/ui/field'
import { Input, Select } from '@/components/ui/input'
import { ToggleChips } from '@/components/ui/toggle-chips'
import { formatLabel } from '@/lib/jobFormat'
import { SALARY_CURRENCIES, type ListCriterion, type TextCriterion } from './criteria'

const LANGUAGE_NAMES: Record<string, string> = {
  en: 'English',
  ka: 'Georgian',
  ru: 'Russian',
  de: 'German',
  fr: 'French',
}

/**
 * Controlled editor for an alert filter. Used by the builder and by the manage page's edit
 * mode, so both produce exactly the same shape.
 */
export function AlertCriteriaForm({
  value,
  onChange,
  options,
  idPrefix = 'alert',
  disabled,
}: {
  value: AlertCriteria
  onChange: (next: AlertCriteria) => void
  options: AlertCriteriaOptions
  idPrefix?: string
  disabled?: boolean
}) {
  const categories = useCategories()

  const categoryNames = useMemo(() => {
    const names = new Map<string, string>()
    for (const c of categories.data ?? []) names.set(c.slug, c.name)
    return names
  }, [categories.data])

  const setText = (key: TextCriterion, text: string) =>
    onChange({ ...value, [key]: text === '' ? null : text })

  const setList = (key: ListCriterion, list: string[]) =>
    onChange({ ...value, [key]: list.length === 0 ? null : list })

  const setNumber = (key: 'salaryMin' | 'salaryMax', raw: string) => {
    const parsed = raw === '' ? null : Number(raw)
    onChange({ ...value, [key]: parsed == null || Number.isNaN(parsed) ? null : parsed })
  }

  const id = (name: string) => `${idPrefix}-${name}`
  const max = options.maxTextLength

  return (
    <div className="space-y-6">
      <FieldGroup legend="What to look for" description="Every filter you set must match. Leave any of them empty to allow anything.">
        <Field
          label="Search text"
          htmlFor={id('query')}
          hint="Matches the same way as the search box: words anywhere in the title, company or description. Use quotes for an exact phrase."
        >
          <Input
            id={id('query')}
            value={value.query ?? ''}
            maxLength={max}
            disabled={disabled}
            placeholder={'e.g. "backend developer" .NET'}
            onChange={(e) => setText('query', e.target.value)}
          />
        </Field>

        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Title contains" htmlFor={id('title')}>
            <Input
              id={id('title')}
              value={value.titleContains ?? ''}
              maxLength={max}
              disabled={disabled}
              onChange={(e) => setText('titleContains', e.target.value)}
            />
          </Field>
          <Field label="Description contains" htmlFor={id('description')}>
            <Input
              id={id('description')}
              value={value.descriptionContains ?? ''}
              maxLength={max}
              disabled={disabled}
              onChange={(e) => setText('descriptionContains', e.target.value)}
            />
          </Field>
          <Field label="Company contains" htmlFor={id('company')}>
            <Input
              id={id('company')}
              value={value.companyContains ?? ''}
              maxLength={max}
              disabled={disabled}
              onChange={(e) => setText('companyContains', e.target.value)}
            />
          </Field>
        </div>
      </FieldGroup>

      <FieldGroup legend="Categories">
        <ToggleChips
          label="Categories"
          options={options.categorySlugs}
          value={value.categorySlugs ?? []}
          onChange={(next) => setList('categorySlugs', next)}
          formatOption={(slug) => categoryNames.get(slug) ?? formatLabel(slug)}
          disabled={disabled}
        />
      </FieldGroup>

      <FieldGroup
        legend="Job details"
        description="These are read from each posting automatically. A job where a detail could not be determined will not match a filter on that detail."
      >
        <ChipRow label="City" options={options.cities} value={value.cities} disabled={disabled}
          onChange={(next) => setList('cities', next)} />
        <ChipRow label="Work mode" options={options.workModes} value={value.workModes} disabled={disabled}
          onChange={(next) => setList('workModes', next)} />
        <ChipRow label="Employment type" options={options.employmentTypes} value={value.employmentTypes} disabled={disabled}
          onChange={(next) => setList('employmentTypes', next)} />
        <ChipRow label="Seniority" options={options.seniorities} value={value.seniorities} disabled={disabled}
          onChange={(next) => setList('seniorities', next)} />
        <ChipRow label="Language" options={options.languages} value={value.languages} disabled={disabled}
          onChange={(next) => setList('languages', next)}
          formatOption={(code) => LANGUAGE_NAMES[code] ?? code.toUpperCase()} />
      </FieldGroup>

      <FieldGroup
        legend="Salary"
        description="Few postings state a salary. Unless you tick the box below, a salary range only narrows jobs that do state one."
      >
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="At least" htmlFor={id('salary-min')}>
            <Input
              id={id('salary-min')}
              type="number"
              min={0}
              inputMode="numeric"
              value={value.salaryMin ?? ''}
              disabled={disabled}
              onChange={(e) => setNumber('salaryMin', e.target.value)}
            />
          </Field>
          <Field label="At most" htmlFor={id('salary-max')}>
            <Input
              id={id('salary-max')}
              type="number"
              min={0}
              inputMode="numeric"
              value={value.salaryMax ?? ''}
              disabled={disabled}
              onChange={(e) => setNumber('salaryMax', e.target.value)}
            />
          </Field>
          <Field label="Currency" htmlFor={id('salary-currency')}>
            <Select
              id={id('salary-currency')}
              value={value.salaryCurrency ?? ''}
              disabled={disabled}
              onChange={(e) => onChange({ ...value, salaryCurrency: e.target.value || null })}
            >
              <option value="">Any</option>
              {SALARY_CURRENCIES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="flex items-center gap-2">
          <input
            id={id('require-salary')}
            type="checkbox"
            className="h-4 w-4 rounded border-input accent-[hsl(var(--primary))]"
            checked={value.requireSalary}
            disabled={disabled}
            onChange={(e) => onChange({ ...value, requireSalary: e.target.checked })}
          />
          <Label htmlFor={id('require-salary')} className="font-normal">
            Only jobs that state a salary
          </Label>
        </div>
      </FieldGroup>
    </div>
  )
}

function ChipRow({
  label,
  options,
  value,
  onChange,
  formatOption = formatLabel,
  disabled,
}: {
  label: string
  options: string[]
  value: string[] | null
  onChange: (next: string[]) => void
  formatOption?: (option: string) => string
  disabled?: boolean
}) {
  if (options.length === 0) return null

  return (
    <div className="grid gap-1.5 sm:grid-cols-[8rem_1fr] sm:items-center">
      <span className="text-sm text-muted-foreground">{label}</span>
      <ToggleChips
        label={label}
        options={options}
        value={value ?? []}
        onChange={onChange}
        formatOption={formatOption}
        disabled={disabled}
      />
    </div>
  )
}
