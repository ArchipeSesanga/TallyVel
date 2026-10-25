import { useQuery } from '@tanstack/react-query'
import { api, unwrap } from './client'

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