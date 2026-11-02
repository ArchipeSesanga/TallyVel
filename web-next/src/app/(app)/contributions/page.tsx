import { Suspense } from 'react'
import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { ContributionCard } from '@/components/ContributionCard'
import { StokvelPicker } from '@/components/StokvelPicker'

// The page itself waits for nothing: it returns the heading and a <Suspense> instantly.
export default function ContributionsPage({
  searchParams,
}: {
  searchParams: Promise<{ stokvel?: string | string[] }>
}) {
  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">Contributions</h1>
      <Suspense fallback={<ContributionsSkeleton />}>
        <Contributions searchParams={searchParams} />
      </Suspense>
    </section>
  )
}

// All the waiting (?stokvel=, the session, the API) happens in here, inside the boundary.
async function Contributions({ searchParams }: { searchParams: Promise<{ stokvel?: string | string[] }> }) {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  if (!home.ok) return <ApiErrorView {...home.problem} />

  const stokvels = home.data.stokvels
  if (stokvels.length === 0) return <p>You&apos;re not in any stokvel yet. Ask a treasurer to add you.</p>

  // The picked stokvel lives in the URL. An unknown id falls back to the first one.
  const { stokvel } = await searchParams
  const chosen = stokvels.find((s) => s.stokvelId === stokvel) ?? stokvels[0]

  // Newest first. The API sends 20 by default; paging comes later.
  const contributions = await readOrFail((init) =>
    api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
      ...init,
      params: { path: { stokvelId: chosen.stokvelId }, query: { sort: '-recordedAt' } },
    }),
  )

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">Contributions in {chosen.name}</h2>
        <StokvelPicker stokvels={stokvels} selectedId={chosen.stokvelId} />
      </div>

      {!contributions.ok ? (
        <ApiErrorView {...contributions.problem} />
      ) : contributions.data.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No contributions yet.</p>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {contributions.data.items.map((c) => (
            // Never shows memberUserId: it is only an id.
            <li key={c.id}>
              <ContributionCard contribution={c} />
            </li>
          ))}
        </ul>
      )}

      {/* Recording lives on the stokvel's own page, where the cycle and what is owed are shown. */}
      <p className="text-sm">
        <Link href={`/stokvels/${chosen.stokvelId}`} className="underline">
          Record a contribution in {chosen.name}
        </Link>
      </p>
    </>
  )
}

// Shaped like the real cards, so nothing jumps when they arrive.
function ContributionsSkeleton() {
  return (
    <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3" aria-busy="true" aria-label="Loading contributions">
      {[1, 2, 3].map((n) => (
        <li key={n} className="h-32 animate-pulse rounded-lg bg-muted" />
      ))}
    </ul>
  )
}
