import type {
  Category,
  JobDetail,
  JobsPage,
  JobsQueryParams,
  ScrapeBatchSummary,
  ScrapeOverview,
  ScrapeRunsPage,
  ScrapeRunsQueryParams,
} from './types'

class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new ApiError(
      response.status === 404 ? 'Not found' : `Request failed (${response.status})`,
      response.status,
    )
  }
  return response.json() as Promise<T>
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
}

export { ApiError }
