import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { formatCents } from '@/lib/money'
import { formatDate } from '@/lib/dates'

// An async Server Component: it waits for its data on the server, then sends finished HTML.
// There is no useEffect and no fetch in the browser; the browser never talks to the API.
export default async function MemberDashboard() {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  // readOrFail RETURNS a problem for 403/429/5xx instead of throwing, because Next.js
  // hides the message of an error thrown from a Server Component in production.
  if (!home.ok) return <ApiErrorView {...home.problem} />

  const stokvels = home.data.stokvels
  if (stokvels.length === 0) {
    // Being in no stokvel is normal for a new person, so this is an empty state, not an error.
    return <p>You&apos;re not in any stokvel yet. Ask a treasurer to add you.</p>
  }

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">Member dashboard</h1>
      <ul className="grid gap-4 md:grid-cols-2">
        {stokvels.map((s) => (
          // The key is the stokvel's database id, never the index, so React keeps the right row.
          <li key={s.stokvelId} className="rounded-lg border border-border p-4">
            <h2 className="font-semibold">{s.name}</h2>
            <p>Paid this cycle: {formatCents(s.paidThisCycleCents)}</p>
            <p>
              {s.owedThisCycleCents === 0
                ? 'All paid up for this cycle'
                : `Still owed: ${formatCents(s.owedThisCycleCents)}`}
            </p>
            <p>Next payout: {s.nextPayoutDate === null ? 'No payout scheduled' : formatDate(s.nextPayoutDate)}</p>
          </li>
        ))}
      </ul>
    </section>
  )
}
