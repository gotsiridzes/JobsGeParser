import type {
  Category,
  JobDetail,
  JobsPage,
  JobsQueryParams,
  ScrapeBatchSummary,
  ScrapeOverview,
  ScrapeRunsPage,
  ScrapeRunsQueryParams,
  SearchPage,
  SearchQueryParams,
} from './types'
import type {
  AlertCriteria,
  AlertCriteriaOptions,
  AlertSubscriptionsResponse,
  ConfirmAlertResponse,
  CreateAlertRequest,
  CreateAlertResponse,
  UpdateAlertRequest,
} from './alertTypes'

class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    /** Field-level messages from the backend, when it sent any. */
    public errors: string[] = [],
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

/** Header that carries an alert capability token. Never put the token in a query string. */
export const ALERT_TOKEN_HEADER = 'X-Alert-Token'

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'DELETE'
  body?: unknown
  token?: string
}

async function readErrorBody(response: Response): Promise<{ message?: string; errors: string[] }> {
  try {
    const body = (await response.json()) as { error?: string; errors?: string[] }
    return { message: body.error, errors: Array.isArray(body.errors) ? body.errors : [] }
  } catch {
    return { errors: [] }
  }
}

function defaultMessage(status: number): string {
  switch (status) {
    case 404:
      return 'Not found'
    case 409:
      return 'That conflicts with something you already have'
    case 429:
      return 'Too many requests, try again later'
    case 503:
      return 'This is temporarily unavailable, try again later'
    default:
      return `Request failed (${status})`
  }
}

async function requestJson<T>(url: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'
  if (options.token) headers[ALERT_TOKEN_HEADER] = options.token

  const response = await fetch(url, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

  if (!response.ok) {
    const { message, errors } = await readErrorBody(response)
    throw new ApiError(errors[0] ?? message ?? defaultMessage(response.status), response.status, errors)
  }

  // 202 and 204 carry no body worth parsing.
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

function fetchJson<T>(url: string): Promise<T> {
  return requestJson<T>(url)
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') {
      search.set(key, String(value))
    }
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const api = {
  getScrapeOverview: () =>
    fetchJson<ScrapeOverview>('/api/jobs/scrape/overview'),

  getScrapeRuns: (params: ScrapeRunsQueryParams = {}) =>
    fetchJson<ScrapeRunsPage>(
      `/api/jobs/scrape/runs${buildQuery({
        status: params.status,
        category: params.category,
        batchId: params.batchId,
        limit: params.limit,
        offset: params.offset,
      })}`,
    ),

  getScrapeRun: (id: number) =>
    fetchJson<import('./types').ScrapeRun>(`/api/jobs/scrape/runs/${id}`),

  getBatch: (batchId: string) =>
    fetchJson<ScrapeBatchSummary>(`/api/jobs/scrape/batches/${batchId}`),

  getCategories: () => fetchJson<Category[]>('/api/jobs/categories'),

  getJobs: (params: JobsQueryParams = {}) =>
    fetchJson<JobsPage>(
      `/api/jobs${buildQuery({
        page: params.page,
        pageSize: params.pageSize,
        category: params.category,
        q: params.q,
      })}`,
    ),

  getJob: (id: number) => fetchJson<JobDetail>(`/api/jobs/${id}`),

  searchJobs: (params: SearchQueryParams) =>
    fetchJson<SearchPage>(
      `/api/search${buildQuery({
        q: params.q,
        category: params.category,
        page: params.page,
        pageSize: params.pageSize,
      })}`,
    ),

  alerts: {
    criteriaOptions: () => fetchJson<AlertCriteriaOptions>('/api/alerts/criteria-options'),

    preview: (criteria: AlertCriteria, page = 1, pageSize = 5) =>
      requestJson<JobsPage>('/api/alerts/preview', {
        method: 'POST',
        body: { criteria, page, pageSize },
      }),

    subscribe: (request: CreateAlertRequest) =>
      requestJson<CreateAlertResponse>('/api/alerts/subscriptions', { method: 'POST', body: request }),

    confirm: (token: string) =>
      requestJson<ConfirmAlertResponse>('/api/alerts/confirm', { method: 'POST', body: { token } }),

    list: (token: string) =>
      requestJson<AlertSubscriptionsResponse>('/api/alerts/subscriptions', { token }),

    update: (token: string, id: string, patch: UpdateAlertRequest) =>
      requestJson<void>(`/api/alerts/subscriptions/${id}`, { method: 'PATCH', body: patch, token }),

    pause: (token: string, id: string) =>
      requestJson<void>(`/api/alerts/subscriptions/${id}/pause`, { method: 'POST', token }),

    resume: (token: string, id: string) =>
      requestJson<void>(`/api/alerts/subscriptions/${id}/resume`, { method: 'POST', token }),

    remove: (token: string, id: string) =>
      requestJson<void>(`/api/alerts/subscriptions/${id}`, { method: 'DELETE', token }),

    unsubscribe: (token: string, subscriptionId?: string) =>
      requestJson<void>('/api/alerts/unsubscribe', {
        method: 'POST',
        body: { token, subscriptionId: subscriptionId ?? null },
      }),
  },
}

export { ApiError }

export function isNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404
}
