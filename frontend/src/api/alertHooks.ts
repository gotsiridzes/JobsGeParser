import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './client'
import type {
  AlertCriteria,
  CreateAlertRequest,
  OpsAlertsQueryParams,
  UpdateAlertRequest,
} from './alertTypes'
import { isCriteriaEmpty } from '@/components/alerts/criteria'

const managedKey = (token: string) => ['alerts', 'managed', token] as const

/**
 * Doubles as the feature flag: the backend only registers alert endpoints when
 * Alerts:Enabled is true, so a failure here means the feature is off and every
 * entry point should stay hidden.
 */
export function useAlertsAvailable() {
  return useQuery({
    queryKey: ['alerts', 'criteria-options'],
    queryFn: api.alerts.criteriaOptions,
    retry: false,
    staleTime: Infinity,
  })
}

/** Pass an already debounced value; every change is a full-text query on the server. */
export function useAlertPreview(criteria: AlertCriteria, enabled = true) {
  return useQuery({
    queryKey: ['alerts', 'preview', criteria],
    queryFn: () => api.alerts.preview(criteria),
    enabled: enabled && !isCriteriaEmpty(criteria),
    placeholderData: keepPreviousData,
    retry: false,
  })
}

export function useCreateAlert() {
  return useMutation({
    mutationFn: (request: CreateAlertRequest) => api.alerts.subscribe(request),
  })
}

export function useConfirmAlert() {
  return useMutation({
    mutationFn: (token: string) => api.alerts.confirm(token),
  })
}

export function useManagedAlerts(token: string) {
  return useQuery({
    queryKey: managedKey(token),
    queryFn: () => api.alerts.list(token),
    enabled: token.length > 0,
    retry: false,
  })
}

function useManagedMutation<TVariables>(
  token: string,
  mutationFn: (variables: TVariables) => Promise<void>,
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: managedKey(token) }),
  })
}

export function useUpdateAlert(token: string) {
  return useManagedMutation(token, ({ id, patch }: { id: string; patch: UpdateAlertRequest }) =>
    api.alerts.update(token, id, patch),
  )
}

export function usePauseAlert(token: string) {
  return useManagedMutation(token, (id: string) => api.alerts.pause(token, id))
}

export function useResumeAlert(token: string) {
  return useManagedMutation(token, (id: string) => api.alerts.resume(token, id))
}

export function useDeleteAlert(token: string) {
  return useManagedMutation(token, (id: string) => api.alerts.remove(token, id))
}

export function useUnsubscribeAll(token: string) {
  return useManagedMutation<void>(token, () => api.alerts.unsubscribe(token))
}

// Ops console: no token, list/detail keyed by id instead of by capability token.

const opsListKey = (params: OpsAlertsQueryParams) => ['ops', 'alerts', params] as const
const opsDetailKey = (id: string) => ['ops', 'alerts', 'detail', id] as const

export function useOpsAlerts(params: OpsAlertsQueryParams) {
  return useQuery({
    queryKey: opsListKey(params),
    queryFn: () => api.opsAlerts.list(params),
  })
}

export function useOpsAlert(id: string) {
  return useQuery({
    queryKey: opsDetailKey(id),
    queryFn: () => api.opsAlerts.get(id),
    enabled: id.length > 0,
  })
}

function useOpsMutation<TVariables>(
  id: string,
  mutationFn: (variables: TVariables) => Promise<void>,
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: opsDetailKey(id) })
      queryClient.invalidateQueries({ queryKey: ['ops', 'alerts'], exact: false })
    },
  })
}

export function useOpsUpdateAlert(id: string) {
  return useOpsMutation<UpdateAlertRequest>(id, (patch) => api.opsAlerts.update(id, patch))
}

export function useOpsPauseAlert(id: string) {
  return useOpsMutation<void>(id, () => api.opsAlerts.pause(id))
}

export function useOpsResumeAlert(id: string) {
  return useOpsMutation<void>(id, () => api.opsAlerts.resume(id))
}

export function useOpsDeleteAlert(id: string) {
  return useOpsMutation<void>(id, () => api.opsAlerts.remove(id))
}
