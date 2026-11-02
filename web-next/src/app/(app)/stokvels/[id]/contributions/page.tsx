import { Suspense } from 'react'
import Link from 'next/link'
import { notFound } from 'next/navigation'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { ContributionHistory } from '@/components/ContributionHistory'
import { HistoryFilters } from '@/components/HistoryFilters'
import { loadHistoryParams } from '@/lib/historyParams'
import { apiQueryFor } from '@/lib/historyQuery'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

type SearchParams = Promise<Record<string, string | string[] | undefined>>

// The page waits for nothing: it returns the heading and a <Suspense> at once. Everything that needs the
// address's ?filters, the session or the API happens inside the boundary.
export default function ContributionHistoryPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>
  searchParams: SearchParams
}) {
  return (
    <Suspense fallback={<HistorySkeleton />}>
      <History params={params} searchParams={searchParams} />
    </Suspense>
  )
}

async function History({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: SearchParams }) {
  const { id } = await params
  if (!UUID.test(id)) notFound() // looks exactly like a stokvel that does not exist

  // The filter comes from the address, read with the SAME parsers the controls and the /bff/ route use.
  // Anything that is not a valid value (?member=banana, ?from=2026-02-30) is already "not set" here.
  const parsed = loadHistoryParams(await searchParams)

  // The two requests the top of the page needs don't depend on each other, so they run together.
  const [stokvel, home] = await Promise.all([
    readOrFail((init) => api.GET('/api/v1/stokvels/{id}', { ...init, params: { path: { id } } })),
    readOrFail((init) => api.GET('/api/v1/me/home', init)),
  ])
  if (!stokvel.ok) return <ApiErrorView {...stokvel.problem} />
  if (!home.ok) return <ApiErrorView {...home.problem} />
  if (!home.data.stokvels.some((s) => s.stokvelId === id)) notFound() // not a member: the same answer as "no such stokvel"

  // A member who is not in THIS stokvel counts as "everyone": no error, the list simply is not narrowed.
  const members = stokvel.data.members.map((m) => ({
    userId: m.userId,
    name: m.fullName ?? 'A member',
  }))
  const member = parsed.member !== null && members.some((m) => m.userId === parsed.member) ? parsed.member : null
  const filter = { ...parsed, member }

  const { query, dateProblem } = apiQueryFor(filter)
  const first = await readOrFail((init) =>
    api.GET('/api/v1/stokvels/{stokvelId}/contributions', { ...init, params: { path: { stokvelId: id }, query } }),
  )

  const filtered = filter.member !== null || filter.from !== null || filter.to !== null

  return (
    <div className="space-y-6">
      <div>
        <p className="text-sm">
          <Link href={`/stokvels/${id}`} className="underline">← {stokvel.data.name}</Link>
        </p>
        <h1 className="mt-2 text-2xl font-bold break-words">Contribution history</h1>
      </div>

      <HistoryFilters members={members} />

      {dateProblem === 'to-before-from' && (
        <p role="status" className="text-sm text-muted-foreground">
          &ldquo;To&rdquo; is before &ldquo;from&rdquo;, so the dates were ignored. Pick the dates again.
        </p>
      )}

      {!first.ok ? (
        <ApiErrorView {...first.problem} />
      ) : first.data.items.length === 0 ? (
        // Not an error: the filter simply matches nothing.
        <div className="space-y-2">
          <p>{filtered ? 'No contributions match these filters.' : 'No contributions yet.'}</p>
          {filtered && (
            <p>
              <Link href={`/stokvels/${id}/contributions`} className="underline">Show everything</Link>
            </p>
          )}
        </div>
      ) : (
        // `key` gives each filter its own list, so one filter's pages can never show under another.
        <ContributionHistory key={JSON.stringify(filter)} stokvelId={id} filter={filter} initial={first.data} />
      )}
    </div>
  )
}

// Shaped like the real thing, so nothing jumps when it arrives.
function HistorySkeleton() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading the contribution history">
      <div className="h-8 w-2/3 animate-pulse rounded bg-muted" />
      <div className="h-32 animate-pulse rounded bg-muted" />
      <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {[1, 2, 3].map((n) => (
          <li key={n} className="h-32 animate-pulse rounded-lg bg-muted" />
        ))}
      </ul>
    </div>
  )
}
