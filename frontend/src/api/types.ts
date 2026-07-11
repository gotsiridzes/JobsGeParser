export type ScrapeRunStatus = 'Running' | 'Completed' | 'Failed'
export type ScrapeRunPhase = 'Discovering' | 'Enriching' | 'Completed' | 'Failed'

export interface ScrapeRun {
  id: number
  batchId: string | null
  categorySlug: string | null
  startedAt: string
  finishedAt: string | null
  inserted: number
  updated: number
  skipped: number
  failed: number
  detailsFetched: number
  detailsSkipped: number
  phase: ScrapeRunPhase
  listingPagesFetched: number
  jobsDiscovered: number
  jobsNeedingDetails: number
  progressUpdatedAt: string | null
  status: ScrapeRunStatus
  errorMessage: string | null
}

export interface ActiveCategoryRun {
  categorySlug: string
  runId: number
}

export interface ScrapeWorkerSnapshot {
  isTickInProgress: boolean
  currentBatchId: string | null
  currentTickStartedAt: string | null
  lastTickStartedAt: string | null
  lastTickCompletedAt: string | null
  lastSkippedTickAt: string | null
  skippedTicks: number
  currentCategorySlug: string | null
  currentRunId: number | null
  activeCategoryRuns: ActiveCategoryRun[]
  categoriesInCurrentTick: string[]
  completedCategoriesInCurrentTick: number
}

export interface ScrapeWorkerStatus {
  scrapeEnabled: boolean
  scrapeIntervalMinutes: number
  scrapeOnStartup: boolean
  worker: ScrapeWorkerSnapshot
}

export interface ScrapeRunsPage {
  items: ScrapeRun[]
  totalCount: number
  limit: number
  offset: number
}

export interface ScrapeBatchSummary {
  batchId: string
  startedAt: string
  finishedAt: string | null
  totalRuns: number
  running: number
  completed: number
  failed: number
  runs: ScrapeRun[]
}

export interface ScrapeOverview {
  worker: ScrapeWorkerStatus
  activeRuns: ScrapeRun[]
  latestPerCategory: ScrapeRun[]
  recentRuns: ScrapeRun[]
  recentBatches: ScrapeBatchSummary[]
}

export interface Category {
  slug: string
  name: string
  listUrl: string
  enabled: boolean
  jobCount: number
  latestScrapeRun: ScrapeRun | null
}

export interface JobStructuredFields {
  salaryMin: number | null
  salaryMax: number | null
  salaryCurrency: string | null
  salaryPeriod: string | null
  city: string | null
  workMode: string | null
  employmentType: string | null
  seniority: string | null
  languageRequirement: string | null
}

export interface JobListItem extends JobStructuredFields {
  id: number
  name: string
  link: string
  company: string
  companyLink: string | null
  published: string
  endDate: string
  lastSeenAt: string
}

export interface JobDetail extends JobStructuredFields {
  id: number
  name: string
  link: string
  company: string
  companyLink: string | null
  published: string
  endDate: string
  description: string | null
  detailsFetchedAt: string | null
  firstSeenAt: string
  lastSeenAt: string
  updatedAt: string | null
  categorySlugs: string[]
  enrichmentVersion: number
  enrichedAt: string | null
}

export interface JobsPage {
  items: JobListItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface JobsQueryParams {
  page?: number
  pageSize?: number
  category?: string
  q?: string
}

export interface SearchResult extends JobStructuredFields {
  id: number
  name: string
  link: string
  company: string
  companyLink: string | null
  published: string
  endDate: string
  lastSeenAt: string
  rank: number | null
}

export interface SearchPage {
  items: SearchResult[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  mode: 'fts' | 'trgm' | string
}

export interface SearchQueryParams {
  q?: string
  category?: string
  page?: number
  pageSize?: number
}

export interface ScrapeRunsQueryParams {
  status?: string
  category?: string
  batchId?: string
  limit?: number
  offset?: number
}
