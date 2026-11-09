import type { Metadata } from 'next'
import { Suspense } from 'react'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { MemberGroups } from '@/components/MemberGroups'
import { todayInSouthAfrica } from '@/lib/dates'
import { groupsFromHome } from '@/lib/groupOverview'

// The browser tab and a screen reader's page title. Each page names itself; the site name is added after it.
export const metadata: Metadata = { title: 'Dashboard' }

// The page itself waits for nothing: it returns the heading and a <Suspense> instantly.
export default function MemberDashboard() {
  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">Member dashboard</h1>
      <Suspense fallback={<StokvelListSkeleton />}>
        <MemberStokvels />
      </Suspense>
    </section>
  )
}

// All the waiting (session + API) happens in here, inside the boundary.
async function MemberStokvels() {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  if (!home.ok) return <ApiErrorView {...home.problem} />

  const stokvels = home.data.stokvels
  if (stokvels.length === 0) return <p>You&apos;re not in any stokvel yet. Ask a treasurer to add you.</p>

  // Server side: work out today's South African date once and pass plain data on. The component does the layout.
  return <MemberGroups groups={groupsFromHome(stokvels)} today={todayInSouthAfrica()} />
}

// Shaped like the real cards, so nothing jumps when the data arrives.
function StokvelListSkeleton() {
  return (
    <ul className="grid gap-4 md:grid-cols-2" aria-busy="true" aria-label="Loading your stokvels">
      {[1, 2].map((n) => (
        <li key={n} className="h-32 animate-pulse rounded-lg bg-muted" />
      ))}
    </ul>
  )
}