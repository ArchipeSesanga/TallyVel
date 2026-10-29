'use client'

import { useRouter } from 'next/navigation'
import type { ApiProblem } from '@/lib/api'

export function ApiErrorView({ message, reference }: ApiProblem) {
  const router = useRouter()
  return (
    <div role="alert" className="space-y-3 rounded-lg border border-border p-4">
      <p>{message}</p>
      <p className="text-sm text-muted-foreground">
        {reference === null ? 'No reference: the API did not answer.' : (
          <>Reference: <code className="select-all font-mono">{reference}</code></>
        )}
      </p>
      {/* refresh() re-runs the Server Components, so the data is fetched again */}
      <button type="button" onClick={() => router.refresh()} className="rounded-md border border-border px-3 py-2 text-sm">
        Try again
      </button>
    </div>
  )
}