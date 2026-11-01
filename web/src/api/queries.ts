import type { RecordContributionRequest } from './types'
import { api, unwrap } from './client'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

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


type RecordContributionInput = {
  body: RecordContributionRequest
  idempotencyKey: string
}

/** Records a contribution, then refreshes everything that shows money for this stokvel. */
export function useRecordContribution(stokvelId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ body, idempotencyKey }: RecordContributionInput) =>
      unwrap(
        await api.POST('/api/v1/stokvels/{stokvelId}/contributions', {
          params: {
            path: { stokvelId },
            header: { 'Idempotency-Key': idempotencyKey },
          },
          body,
        }),
      ),
    // Returning the promise makes mutateAsync wait until the queries have refetched,
    // so the button stays disabled until the screen shows the new totals.
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.home }),
        queryClient.invalidateQueries({ queryKey: queryKeys.contributions(stokvelId) }),
      ]),
  })
}