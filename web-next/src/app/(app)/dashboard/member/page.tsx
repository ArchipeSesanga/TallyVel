import { Suspense } from 'react'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { formatCents } from '@/lib/money'
import { formatDate } from '@/lib/dates'

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

  return (
    <ul className="grid gap-4 md:grid-cols-2">
      {stokvels.map((s) => (
        <li key={s.stokvelId} className="rounded-lg border border-border p-4">
          <h2 className="font-semibold">{s.name}</h2>
          <p>Paid this cycle: {formatCents(s.paidThisCycleCents)}</p>
          <p>{s.owedThisCycleCents === 0 ? 'All paid up for this cycle' : `Still owed: ${formatCents(s.owedThisCycleCents)}`}</p>
          <p>Next payout: {s.nextPayoutDate === null ? 'No payout scheduled' : formatDate(s.nextPayoutDate)}</p>
        </li>
      ))}
    </ul>
  )
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