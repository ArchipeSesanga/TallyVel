import { Suspense } from 'react'
import { api, readOrFail } from '@/lib/api'
import { ActivityFeed } from '@/components/ActivityFeed'
import { ApiErrorView } from '@/components/ApiErrorView'

// The page waits for nothing: the heading and a <Suspense> go out at once.
export default function ActivityPage() {
  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">Activity</h1>
      <p className="text-sm text-muted-foreground">What happened in your stokvels, newest first.</p>
      <Suspense fallback={<ActivitySkeleton />}>
        <FirstPage />
      </Suspense>
    </section>
  )
}

// Page one is rendered here, on the server. The pages after it are loaded in the browser through /bff/activity.
async function FirstPage() {
  const first = await readOrFail((init) => api.GET('/api/v1/me/activity', init))
  if (!first.ok) return <ApiErrorView {...first.problem} />
  if (first.data.items.length === 0) return <p className="text-muted-foreground">Nothing has happened in your stokvels yet.</p>
  return <ActivityFeed initial={first.data} />
}

function ActivitySkeleton() {
  return (
    <ul className="divide-y divide-border rounded-lg border border-border" aria-busy="true" aria-label="Loading your activity">
      {[1, 2, 3, 4, 5].map((n) => (
        <li key={n} className="flex justify-between gap-2 p-3">
          <span className="h-5 w-56 animate-pulse rounded bg-muted" />
          <span className="h-5 w-28 animate-pulse rounded bg-muted" />
        </li>
      ))}
    </ul>
  )
}
