import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '../auth/session'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // TanStack retries every failure 3 times by default. A 403, 404 or 429 won't fix
      // itself, and retrying a 429 makes the rate limit worse, so never retry a 4xx.
      retry: (failureCount, error) => {
        const isClientError = error instanceof ApiError && error.status >= 400 && error.status < 500
        return !isClientError && failureCount < 3
      },
    },
  },
})