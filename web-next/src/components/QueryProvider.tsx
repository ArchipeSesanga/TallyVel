'use client'

import { useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BffRequestError } from '@/lib/bffClient'

/**
 * TanStack Query's cache for the pages loaded AFTER page one (the first page is rendered by the server).
 * The client is made once per browser tab with useState, never at module level: a module-level client would
 * be shared by every visitor on the server and leak one person's data to the next.
 */
export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // A 4xx will not get better by asking again (a wrong filter, no access). A 5xx or a lost
            // connection might, so those get two more tries.
            retry: (failureCount, error) =>
              !(error instanceof BffRequestError && error.status >= 400 && error.status < 500) && failureCount < 2,
            refetchOnWindowFocus: false, // a list the person is reading should not jump when they switch tabs
          },
        },
      }),
  )

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}
