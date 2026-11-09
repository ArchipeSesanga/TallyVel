import { Suspense } from 'react'
import { notFound } from 'next/navigation'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { getSession } from '@/lib/session'
import { recordContribution } from '@/app/actions'
import { FlashToast } from '@/components/FlashToast'
import { RecentContributionsList } from '@/components/RecentContributionsList'
import { StokvelDetail } from '@/components/StokvelDetail'
import { stokvelViewOf } from '@/lib/stokvelView'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// NEW: the page is no longer async. It returns a <Suspense> straight away, so a click
// on a stokvel shows the skeleton instantly instead of waiting for the API.
export default function StokvelDetailPage({ params, searchParams }: PageProps<'/stokvels/[id]'>) {
  return (
    <Suspense fallback={<HeaderSkeleton />}>
      <StokvelDetailData params={params} searchParams={searchParams} />
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

async function StokvelDetailData({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const flags = await searchParams
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

  const session = await getSession() // the page is inside <Suspense>, so reading the cookie is fine
  if (session === null) notFound() // readOrFail has already sent a signed-out person to /login; this only narrows the type

  // What the page shows and offers is worked out from the API's two answers by one plain function (lib/stokvelView.ts).
  const view = stokvelViewOf(stokvel.data, entry)

  return (
    <StokvelDetail
      view={view}
      youId={session.user.id}
      recordAction={recordContribution}
      recordKey={crypto.randomUUID()}
      closeKey={crypto.randomUUID()}
      flash={
        <>
          {flags.joined === '1' && <FlashToast message={`You joined ${view.name}.`} />}
          {flags.created === '1' && <FlashToast message={`Created ${view.name}. You are its Chairperson.`} />}
        </>
      }
      recent={
        <Suspense fallback={<RecentSkeleton />}>
          <RecentContributions
            recentPromise={recentPromise}
            stokvelId={id}
            youId={session.user.id}
            canRun={view.canRun}
            openCycleLabel={view.cycle?.label ?? null}
            names={Object.fromEntries(view.members.map((m) => [m.userId, m.fullName ?? 'A member']))}
          />
        </Suspense>
      }
    />
  )
}

// Awaits the request that was ALREADY started above, so it has been running all along.
async function RecentContributions({
  recentPromise, ...rest
}: { recentPromise: ReturnType<typeof readRecent> } & Omit<React.ComponentProps<typeof RecentContributionsList>, 'items'>) {
  const recent = await recentPromise
  if (!recent.ok) return <ApiErrorView {...recent.problem} />
  return <RecentContributionsList items={recent.data.items} {...rest} />
}

function HeaderSkeleton() {
  return (
    <div className="flex w-full max-w-[960px] flex-col gap-6 px-12 pt-10 pb-12 max-sm:px-4" aria-busy="true" aria-label="Loading the stokvel">
      <div className="h-8 w-32 animate-pulse rounded-lg bg-muted" />
      <div className="h-16 w-2/3 animate-pulse rounded-2xl bg-muted" />
      <div className="h-28 animate-pulse rounded-2xl bg-muted" />
    </div>
  )
}

// Shaped like the real rows, so nothing jumps when they arrive.
function RecentSkeleton() {
  return (
    <ul className="divide-y divide-divider" aria-busy="true" aria-label="Loading recent contributions">
      {[1, 2, 3].map((n) => (
        <li key={n} className="flex items-center justify-between gap-3 py-3.5">
          <span className="h-10 w-40 animate-pulse rounded-xl bg-muted" />
          <span className="h-5 w-16 animate-pulse rounded bg-muted" />
        </li>
      ))}
    </ul>
  )
}
