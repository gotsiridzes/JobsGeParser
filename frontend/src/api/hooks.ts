import { useQuery } from '@tanstack/react-query'
import { api } from './client'
import type { JobsQueryParams, ScrapeRunsQueryParams, SearchQueryParams } from './types'

export function useScrapeOverview() {
  return useQuery({
    queryKey: ['scrape', 'overview'],
    queryFn: api.getScrapeOverview,
    refetchInterval: (query) => {
      const data = query.state.data
      if (!data) return 30_000
      const isActive =
        data.worker.worker.isTickInProgress ||
        data.activeRuns.length > 0
      return isActive ? 5_000 : 30_000
    },
  })
}

export function useScrapeRuns(params: ScrapeRunsQueryParams) {
  return useQuery({
    queryKey: ['scrape', 'runs', params],
    queryFn: () => api.getScrapeRuns(params),
  })
}

export function useBatch(batchId: string) {
  return useQuery({
    queryKey: ['scrape', 'batch', batchId],
    queryFn: () => api.getBatch(batchId),
    enabled: !!batchId,
  })
}

export function useCategories() {
  return useQuery({
    queryKey: ['categories'],
    queryFn: api.getCategories,
  })
}

export function useJobs(params: JobsQueryParams) {
  return useQuery({
    queryKey: ['jobs', params],
    queryFn: () => api.getJobs(params),
  })
}

export function useJob(id: number) {
  return useQuery({
    queryKey: ['job', id],
    queryFn: () => api.getJob(id),
    enabled: id > 0,
  })
}

export function useSearch(params: SearchQueryParams) {
  const q = params.q?.trim() ?? ''
  return useQuery({
    queryKey: ['search', params],
    queryFn: () => api.searchJobs({ ...params, q }),
    enabled: q.length > 0,
  })
}
