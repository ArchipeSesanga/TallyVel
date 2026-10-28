import { notFound } from 'next/navigation'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { formatCents } from '@/lib/money'
import { formatDate } from '@/lib/dates'
import { roleLabel } from '@/lib/roles'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export default async function StokvelDetailPage({ params }: PageProps<'/stokvels/[id]'>) {
  // params is a Promise in Next.js 16, so it has to be awaited.
  const { id } = await params

  // A malformed id gets notFound() BEFORE we call the API, so "not-a-guid" looks exactly
  // like a stokvel that doesn't exist (and the API is not asked a question it can only reject).
  if (!UUID.test(id)) notFound()

  // The two requests don't depend on each other, so they run at the same time.
  const [stokvel, home] = await Promise.all([
    readOrFail((init) => api.GET('/api/v1/stokvels/{id}', { ...init, params: { path: { id } } })),
    readOrFail((init) => api.GET('/api/v1/me/home', init)),
  ])
  if (!stokvel.ok) return <ApiErrorView {...stokvel.problem} />
  if (!home.ok) return <ApiErrorView {...home.problem} />

  // The person's role in THIS stokvel comes from their /me/home entry. No entry means they
  // aren't a member, so we answer exactly as for a missing stokvel: it must not reveal that it exists.
  const entry = home.data.stokvels.find((s) => s.stokvelId === id)
  if (entry === undefined) notFound()

  const { name, contributionAmountCents, memberCount, currentCycle } = stokvel.data

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">{name}</h1>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
        <dt className="text-muted-foreground">Contribution</dt>
        <dd>{formatCents(contributionAmountCents)}</dd>

        <dt className="text-muted-foreground">Your role</dt>
        <dd>{roleLabel(entry.role)}</dd>

        {/* The API already counted the members; the page never counts or adds anything up. */}
        <dt className="text-muted-foreground">Members</dt>
        <dd>{memberCount}</dd>

        <dt className="text-muted-foreground">Current cycle</dt>
        <dd>
          {currentCycle === null ? (
            'No open cycle'
          ) : (
            <>
              {currentCycle.label}: {formatCents(currentCycle.collectedCents)} collected of{' '}
              {formatCents(currentCycle.dueCents)}
              {currentCycle.endsOn !== null && <> (due {formatDate(currentCycle.endsOn)})</>}
            </>
          )}
        </dd>
      </dl>
    </section>
  )
}
