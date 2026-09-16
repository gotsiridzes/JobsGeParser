import type { AlertCriteria } from '@/api/alertTypes'

export const SALARY_CURRENCIES = ['GEL', 'USD', 'EUR'] as const

export type ListCriterion =
  | 'categorySlugs'
  | 'cities'
  | 'workModes'
  | 'employmentTypes'
  | 'seniorities'
  | 'languages'

export type TextCriterion = 'query' | 'titleContains' | 'descriptionContains' | 'companyContains'

export function emptyCriteria(): AlertCriteria {
  return {
    query: null,
    titleContains: null,
    descriptionContains: null,
    companyContains: null,
    categorySlugs: null,
    cities: null,
    workModes: null,
    employmentTypes: null,
    seniorities: null,
    languages: null,
    salaryMin: null,
    salaryMax: null,
    salaryCurrency: null,
    requireSalary: false,
  }
}

const blank = (value: string | null) => value == null || value.trim() === ''
const unset = (values: string[] | null) => values == null || values.length === 0

/** Mirrors AlertCriteria.IsEmpty on the backend: an empty filter would match every job. */
export function isCriteriaEmpty(c: AlertCriteria): boolean {
  return (
    blank(c.query) &&
    blank(c.titleContains) &&
    blank(c.descriptionContains) &&
    blank(c.companyContains) &&
    unset(c.categorySlugs) &&
    unset(c.cities) &&
    unset(c.workModes) &&
    unset(c.employmentTypes) &&
    unset(c.seniorities) &&
    unset(c.languages) &&
    c.salaryMin == null &&
    c.salaryMax == null
  )
}

/**
 * Seeds a filter from the search page. The search box maps to `query` because the backend
 * matches it with the same full-text semantics as /api/search.
 */
export function criteriaFromSearchParams(q: string | null, category: string | null): AlertCriteria {
  return {
    ...emptyCriteria(),
    query: q?.trim() ? q.trim() : null,
    categorySlugs: category ? [category] : null,
  }
}

/** Normalizes blank strings to null and empty lists to null before sending. */
export function cleanCriteria(c: AlertCriteria): AlertCriteria {
  const text = (value: string | null) => (blank(value) ? null : value!.trim())
  const list = (values: string[] | null) => (unset(values) ? null : values)
  const hasSalary = c.salaryMin != null || c.salaryMax != null || c.requireSalary

  return {
    query: text(c.query),
    titleContains: text(c.titleContains),
    descriptionContains: text(c.descriptionContains),
    companyContains: text(c.companyContains),
    categorySlugs: list(c.categorySlugs),
    cities: list(c.cities),
    workModes: list(c.workModes),
    employmentTypes: list(c.employmentTypes),
    seniorities: list(c.seniorities),
    languages: list(c.languages),
    salaryMin: c.salaryMin,
    salaryMax: c.salaryMax,
    // The backend rejects a currency with nothing for it to qualify.
    salaryCurrency: hasSalary ? c.salaryCurrency : null,
    requireSalary: c.requireSalary,
  }
}

/** "09:00" to "09:00:00", the TimeOnly shape the backend binds unambiguously. */
export function toTimeOnly(hhmm: string): string {
  return /^\d{2}:\d{2}$/.test(hhmm) ? `${hhmm}:00` : hhmm
}

/** "09:00:00" to "09:00" for a time input. */
export function fromTimeOnly(value: string): string {
  return value.slice(0, 5)
}

export function browserTimeZone(): string | null {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone ?? null
  } catch {
    return null
  }
}

export function defaultAlertName(c: AlertCriteria): string {
  const parts = [c.query, c.titleContains, c.categorySlugs?.join(', ')].filter(
    (p): p is string => !!p && p.trim() !== '',
  )
  const name = parts.length > 0 ? parts.join(' in ') : 'My job alert'
  return name.slice(0, 100)
}
