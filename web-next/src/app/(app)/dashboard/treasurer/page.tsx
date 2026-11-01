import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { StokvelCard } from '@/components/StokvelCard'
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
        <h2 className="text-lg font-semibold">Treasurer dashboard</h2>
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
    <>
      <h2 className="mb-4 text-lg font-semibold">Stokvels you run</h2>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {mine.map((s) => (
          <div key={s.stokvelId} className="space-y-2">
            <StokvelCard entry={s} />
            <Link href={`/stokvels/${s.stokvelId}`} className="text-sm underline">
              View {s.name}
            </Link>
          </div>
        ))}
      </div>
    </>
  )
}
