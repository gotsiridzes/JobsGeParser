export function formatSalary(
  min: number | null | undefined,
  max: number | null | undefined,
  currency: string | null | undefined,
  period: string | null | undefined,
): string | null {
  if (min == null && max == null) return null
  const cur = currency ?? ''
  const range =
    min != null && max != null && min !== max
      ? `${formatAmount(min)}-${formatAmount(max)}`
      : formatAmount(min ?? max ?? 0)
  const periodLabel = period ? `/${period === 'month' ? 'mo' : period === 'hour' ? 'hr' : period}` : ''
  return `${range} ${cur}${periodLabel}`.trim()
}

function formatAmount(n: number): string {
  return Number.isInteger(n) ? String(n) : n.toFixed(0)
}

export function formatLabel(value: string): string {
  return value.replace(/_/g, ' ')
}
