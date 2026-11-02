import { Suspense } from 'react'
import { notFound } from 'next/navigation'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { formatCents } from '@/lib/money'
import { formatDate, formatDateTime } from '@/lib/dates'
import { canRunCycle, roleLabel } from '@/lib/roles'
import { centsToInput } from '@/lib/moneyInput'
import { getSession } from '@/lib/session'
import { ContributionForm } from '@/components/ContributionForm'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// NEW: the page is no longer async. It returns a <Suspense> straight away, so a click
// on a stokvel shows the skeleton instantly instead of waiting for the API.
export default function StokvelDetailPage({ params }: PageProps<'/stokvels/[id]'>) {
  return (
    <Suspense fallback={<HeaderSkeleton />}>
      <StokvelDetail params={params} />
    </Suspense>
  )
}

// NEW: the "latest 5, newest first" request, in one place. Its return type also types the prop below.
function readRecent(id: string) {
  return readOrFail((init) =>
    api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
      ...init,
      params: { path: { stokvelId: id }, query: { pageSize: 5, sort: '-recordedAt' } },
    }),
  )
}

async function StokvelDetail({ params }: { params: Promise<{ id: string }> }) {
  // params is a Promise in Next.js 16, so it has to be awaited.
  const { id } = await params

  // A malformed id gets notFound() BEFORE we call the API, so "not-a-guid" looks exactly
  // like a stokvel that doesn't exist (and the API is not asked a question it can only reject).
  if (!UUID.test(id)) notFound()

  // NEW: START the recent-contributions request now, but don't wait for it here.
  // It runs at the same time as the two below; only <RecentContributions> awaits it.
  const recentPromise = readRecent(id)
  // If this component stops early (not found, an error), nobody awaits recentPromise.
  // This stops Node reporting that as an unhandled rejection; awaiting it later still works.
  recentPromise.catch(() => {})

  // The two requests the top of the page needs don't depend on each other, so they run at the same time.
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

  const { name, contributionAmountCents, memberCount, currentCycle, members } = stokvel.data
  const session = await getSession() // the page is inside <Suspense>, so reading the cookie is fine
  if (session === null) notFound() // readOrFail has already sent a signed-out person to /login; this only narrows the type

  return (
    <div className="space-y-8">
      <section className="space-y-4">
        <h1 className="text-2xl font-bold break-words">{name}</h1>
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
          <dt className="text-muted-foreground">Contribution</dt>
          <dd className="whitespace-nowrap">{formatCents(contributionAmountCents)}</dd>

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

      {/* Only while the cycle is open. THIS stokvel's role comes from the API (/me/home), never from the session. */}
      {currentCycle !== null && currentCycle.status === 'Open' && (
        <section className="space-y-3">
          <h2 className="text-xl font-semibold">Record a contribution</h2>
          <ContributionForm
            stokvelId={id}
            cycle={currentCycle.label}
            youId={session.user.id}
            members={
              canRunCycle(entry.role)
                ? members.map((m) => ({ userId: m.userId, name: m.fullName ?? 'A member' }))
                : null
            }
            prefill={entry.owedThisCycleCents > 0 ? centsToInput(entry.owedThisCycleCents) : ''}
            firstKey={crypto.randomUUID()}
          />
        </section>
      )}

      {/* NEW: its own boundary, so the top of the page never waits for this list. */}
      <section className="space-y-3">
        <h2 className="text-xl font-semibold">Recent contributions</h2>
        <Suspense fallback={<RecentSkeleton />}>
          <RecentContributions recentPromise={recentPromise} />
        </Suspense>
      </section>
    </div>
  )
}

// NEW: awaits the request that was ALREADY started above, so it has been running all along.
async function RecentContributions({ recentPromise }: { recentPromise: ReturnType<typeof readRecent> }) {
  const recent = await recentPromise
  if (!recent.ok) return <ApiErrorView {...recent.problem} />

  if (recent.data.items.length === 0) {
    return <p className="text-muted-foreground">No contributions yet. They&apos;ll appear here as members pay.</p>
  }

  return (
    <ul className="divide-y divide-border rounded-lg border border-border">
      {recent.data.items.map((c) => (
        // Only what a person needs: amount, status, when, which cycle. Never memberUserId.
        <li key={c.id} className="flex flex-wrap items-baseline justify-between gap-2 p-3">
          <span className="font-medium whitespace-nowrap">{formatCents(c.amountCents)}</span>
          <span>{c.status}</span>
          <span className="text-sm text-muted-foreground">
            {formatDateTime(c.recordedAt)} · cycle {c.cycle}
          </span>
        </li>
      ))}
    </ul>
  )
}

function HeaderSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true" aria-label="Loading the stokvel">
      <div className="h-8 w-2/3 animate-pulse rounded bg-muted" />
      <div className="h-24 animate-pulse rounded bg-muted" />
    </div>
  )
}

// Shaped like the real rows, so nothing jumps when they arrive.
function RecentSkeleton() {
  return (
    <ul className="divide-y divide-border rounded-lg border border-border" aria-busy="true" aria-label="Loading recent contributions">
      {[1, 2, 3, 4, 5].map((n) => (
        <li key={n} className="flex justify-between gap-2 p-3">
          <span className="h-5 w-20 animate-pulse rounded bg-muted" />
          <span className="h-5 w-16 animate-pulse rounded bg-muted" />
          <span className="h-5 w-32 animate-pulse rounded bg-muted" />
        </li>
      ))}
    </ul>
  )
}