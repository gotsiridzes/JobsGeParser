import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './client'
import type { AlertCriteria, CreateAlertRequest, UpdateAlertRequest } from './alertTypes'
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
