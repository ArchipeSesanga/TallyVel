import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { formatCents } from '@/lib/money'
import { formatDate } from '@/lib/dates'
import { stokvelsWhereTreasurer } from '@/lib/treasurer'

export default async function TreasurerDashboard() {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  if (!home.ok) return <ApiErrorView {...home.problem} />

  // The role comes from the API, one per stokvel (/me/home), never from the session or a token.
  // A person can be Treasurer in one stokvel and a Member in another, and the API is what knows.
  const mine = stokvelsWhereTreasurer(home.data.stokvels)

  if (mine.length === 0) {
    // Not being a treasurer is normal, so this is a helpful empty state. It is not a 403 and
    // not a redirect: nothing was forbidden, this person just doesn't run a stokvel yet.
    return (
      <section className="space-y-2">
        <h1 className="text-2xl font-bold">Treasurer dashboard</h1>
        <p>You don&apos;t run any stokvels yet. When you are made Treasurer of one, it will show up here.</p>
        <p>
          <Link href="/dashboard/member" className="underline">
            Go to your member dashboard
          </Link>
        </p>
      </section>
    )
  }

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">Treasurer dashboard</h1>
      <ul className="grid gap-4 md:grid-cols-2">
        {mine.map((s) => (
          <li key={s.stokvelId} className="rounded-lg border border-border p-4">
            <h2 className="font-semibold">
              <Link href={`/stokvels/${s.stokvelId}`} className="underline">
                {s.name}
              </Link>
            </h2>
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
