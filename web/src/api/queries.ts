import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, unwrap } from './client'
import type { RecordContributionRequest } from './types'

// Every key in one place. Everything about one stokvel starts with ['stokvels', id],
// so refreshing one stokvel never touches another.
export const queryKeys = {
  me: ['me'] as const,
  home: ['me', 'home'] as const,
  stokvel: (id: string) => ['stokvels', id] as const,
  contributions: (id: string) => ['stokvels', id, 'contributions'] as const,
}

/** The signed-in user's own profile (GET /api/v1/me). */
export function useMe() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: async () => unwrap(await api.GET('/api/v1/me')),
  })
}

/** The signed-in member's stokvels, each with their own role (GET /api/v1/me/home). */
export function useHome() {
  return useQuery({
    queryKey: queryKeys.home,
    queryFn: async () => unwrap(await api.GET('/api/v1/me/home')).stokvels,
  })
}

/** One stokvel's contributions. First page only for now; paging comes later. */
export function useContributions(stokvelId: string) {
  return useQuery({
    queryKey: queryKeys.contributions(stokvelId),
    // An empty id means the stokvel list has not loaded yet; do not call the API with it.
    enabled: stokvelId !== '',
    queryFn: async () =>
      unwrap(
        await api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
          params: { path: { stokvelId } },
        }),
      ).items,
  })
}

/**
 * Records a contribution (POST /api/v1/stokvels/{id}/contributions). The caller owns the
 * idempotency key: retrying the same payment must send the same key, so it is passed in.
 */
export function useRecordContribution(stokvelId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { idempotencyKey: string; body: RecordContributionRequest }) =>
      unwrap(
        await api.POST('/api/v1/stokvels/{stokvelId}/contributions', {
          params: { path: { stokvelId }, header: { 'Idempotency-Key': input.idempotencyKey } },
          body: input.body,
        }),
      ),
    // The list and the paid/owed figures on the cards are now out of date.
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.contributions(stokvelId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.home }),
      ])
    },
  })
}
