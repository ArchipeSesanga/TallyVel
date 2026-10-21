import { useQuery } from '@tanstack/react-query'
import { api, unwrap } from './client'

// Every key in one place. Everything about one stokvel starts with ['stokvels', id],
// so refreshing one stokvel never touches another.
export const queryKeys = {
  home: ['me', 'home'] as const,
  stokvel: (id: string) => ['stokvels', id] as const,
  contributions: (id: string) => ['stokvels', id, 'contributions'] as const,
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
    queryFn: async () =>
      unwrap(
        await api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
          params: { path: { stokvelId } },
        }),
      ).items,
  })
}