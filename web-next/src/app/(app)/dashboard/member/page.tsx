import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { ContributionCard } from '@/components/ContributionCard'
import { StokvelCard } from '@/components/StokvelCard'
import { StokvelPicker } from '@/components/StokvelPicker'

// An async Server Component: it waits for its data on the server, then sends finished HTML.
// There is no useEffect and no fetch in the browser; the browser never talks to the API.
export default async function MemberDashboard({
  searchParams,
}: {
  searchParams: Promise<{ stokvel?: string }>
}) {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  // readOrFail RETURNS a problem for 403/429/5xx instead of throwing, because Next.js
  // hides the message of an error thrown from a Server Component in production.
  if (!home.ok) return <ApiErrorView {...home.problem} />

  const stokvels = home.data.stokvels
  if (stokvels.length === 0) {
    // Being in no stokvel is normal for a new person, so this is an empty state, not an error.
    return <p className="text-sm text-muted-foreground">You&apos;re not in any stokvel yet. Ask a treasurer to add you.</p>
  }

  // The contributions follow the picked stokvel; an unknown id in the URL falls back to the first one.
  const { stokvel } = await searchParams
  const chosen = stokvels.find((s) => s.stokvelId === stokvel) ?? stokvels[0]
  const contributions = await readOrFail((init) =>
    api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
      ...init,
      params: { path: { stokvelId: chosen.stokvelId } },
    }),
  )

  return (
    <>
      <h2 className="mb-4 text-lg font-semibold">My stokvels</h2>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {stokvels.map((s) => (
          // The key is the database id, not the array index.
          <StokvelCard key={s.stokvelId} entry={s} />
        ))}
      </div>

      <div className="mt-8 mb-4 flex flex-wrap items-center justify-between gap-2">
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
            <li key={c.id}>
              <ContributionCard contribution={c} />
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
