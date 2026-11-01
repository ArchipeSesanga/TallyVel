import { Suspense } from 'react'
import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { StokvelCard } from '@/components/StokvelCard'

// Not async: returns the heading and a <Suspense> instantly, so the page never blocks.
export default function StokvelsPage() {
  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">My stokvels</h1>
      <Suspense fallback={<StokvelCardsSkeleton />}>
        <StokvelList />
      </Suspense>
    </section>
  )
}

// All the waiting (session + API) happens inside the boundary.
async function StokvelList() {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  if (!home.ok) return <ApiErrorView {...home.problem} />

  const stokvels = home.data.stokvels
  if (stokvels.length === 0) return <p>You're not in any stokvel yet. Ask a treasurer to add you.</p>

  return (
    <ul className="grid gap-4 md:grid-cols-2">
      {stokvels.map((entry) => (
        <li key={entry.stokvelId}>
          <Link href={`/stokvels/${entry.stokvelId}`} className="block rounded-lg focus-visible:ring-2 focus-visible:ring-ring">
            <StokvelCard entry={entry} />
          </Link>
        </li>
      ))}
    </ul>
  )
}

// Shaped like the real cards, so nothing jumps when they arrive.
function StokvelCardsSkeleton() {
  return (
    <ul className="grid gap-4 md:grid-cols-2" aria-busy="true" aria-label="Loading your stokvels">
      {[1, 2].map((n) => (
        <li key={n} className="h-48 animate-pulse rounded-lg bg-muted" />
      ))}
    </ul>
  )
}