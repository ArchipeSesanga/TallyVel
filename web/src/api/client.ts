import createClient from 'openapi-fetch'
import type { paths } from './schema'
import { ApiError, authFetch } from '../auth/session'

// One client for the whole app. Every request goes through authFetch,
// so every request gets the token, the refresh and the single retry.
export const api = createClient<paths>({
  baseUrl: import.meta.env.VITE_API_BASE_URL,
  fetch: authFetch,
})

/** Returns the data, or throws an ApiError with the status, the problem+json body and Retry-After. */
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.error !== undefined || result.data === undefined) {
    throw new ApiError(
      result.response.status,
      result.error ?? null,
      result.response.headers.get('Retry-After'),
    )
  }
  return result.data
}
