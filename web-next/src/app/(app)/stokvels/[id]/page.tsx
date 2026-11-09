import { Suspense } from 'react'
import Link from 'next/link'
import { notFound } from 'next/navigation'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { formatCents } from '@/lib/money'
import { formatDate, formatDateTime } from '@/lib/dates'
import { canRunCycle, roleLabel } from '@/lib/roles'
import { centsToInput } from '@/lib/moneyInput'
import { getSession } from '@/lib/session'
import { ContributionForm } from '@/components/ContributionForm'
import { PinToggle } from '@/components/PinToggle'
import { CreateInviteLink } from '@/components/CreateInviteLink'
import { ProofControls } from '@/components/ProofControls'
import { CloseCycleDialog } from '@/components/CloseCycleDialog'
import { FlashToast } from '@/components/FlashToast'
import { LeaveStokvelDialog } from '@/components/LeaveStokvelDialog'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// NEW: the page is no longer async. It returns a <Suspense> straight away, so a click
// on a stokvel shows the skeleton instantly instead of waiting for the API.
export default function StokvelDetailPage({ params, searchParams }: PageProps<'/stokvels/[id]'>) {
  return (
    <Suspense fallback={<HeaderSkeleton />}>
      <StokvelDetail params={params} searchParams={searchParams} />
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

async function StokvelDetail({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
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

  const { name, contributionAmountCents, memberCount, currentCycle, members } = stokvel.data
  const session = await getSession() // the page is inside <Suspense>, so reading the cookie is fine
  if (session === null) notFound() // readOrFail has already sent a signed-out person to /login; this only narrows the type

  return (
    <div className="space-y-8">
      {flags.joined === '1' && <FlashToast message={`You joined ${name}.`} />}
      {flags.created === '1' && <FlashToast message={`Created ${name}. You are its Chairperson.`} />}
      <section className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h1 id="stokvel-heading" tabIndex={-1} className="text-2xl font-bold break-words outline-none">{name}</h1>
          <PinToggle stokvelId={id} name={name} />
        </div>
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

      {/* Leave: any member. The only Chairperson can't (the API refuses), so we say why instead of offering a button that fails. */}
      <section className="space-y-2">
        {entry.role === 'Chairperson' && members.filter((m) => m.role === 'Chairperson').length === 1 ? (
          <p className="text-sm text-muted-foreground">
            You are the only Chairperson, so you can&apos;t leave yet. Another member must be made Chairperson first.
          </p>
        ) : (
          <LeaveStokvelDialog stokvelId={id} stokvelName={name} />
        )}
      </section>

      {/* Close the cycle: THIS stokvel's Treasurer only, and only while the cycle is open. */}
      {entry.role === 'Treasurer' && currentCycle !== null && currentCycle.status === 'Open' && (
        <section className="space-y-3">
          <h2 className="text-xl font-semibold">Close the cycle</h2>
          <CloseCycleDialog stokvelId={id} cycleId={currentCycle.id} cycleLabel={currentCycle.label} firstKey={crypto.randomUUID()} />
        </section>
      )}

      {/* Treasurer-only on purpose (the API also lets a Chairperson do this; the UI is stricter). THIS stokvel's role, from the API. */}
      {entry.role === 'Treasurer' && (
        <section className="space-y-3">
          <h2 className="text-xl font-semibold">Invite someone</h2>
          <CreateInviteLink stokvelId={id} />
        </section>
      )}

      {/* NEW: its own boundary, so the top of the page never waits for this list. */}
      <section className="space-y-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-xl font-semibold">Recent contributions</h2>
          <Link href={`/stokvels/${id}/contributions`} className="text-sm underline">See the full history</Link>
        </div>
        <Suspense fallback={<RecentSkeleton />}>
          <RecentContributions
            recentPromise={recentPromise}
            stokvelId={id}
            youId={session.user.id}
            // Treasurer only, like the other controls here (the API would also allow a Chairperson).
            isTreasurer={entry.role === 'Treasurer'}
            openCycleLabel={currentCycle !== null && currentCycle.status === 'Open' ? currentCycle.label : null}
            names={Object.fromEntries(members.map((m) => [m.userId, m.fullName ?? 'A member']))}
          />
        </Suspense>
      </section>
    </div>
  )
}

// NEW: awaits the request that was ALREADY started above, so it has been running all along.
async function RecentContributions({
  recentPromise, stokvelId, youId, isTreasurer, openCycleLabel, names,
}: {
  recentPromise: ReturnType<typeof readRecent>
  stokvelId: string
  youId: string
  isTreasurer: boolean
  openCycleLabel: string | null
  names: Record<string, string>
}) {
  const recent = await recentPromise
  if (!recent.ok) return <ApiErrorView {...recent.problem} />

  if (recent.data.items.length === 0) {
    return <p className="text-muted-foreground">No contributions yet. They&apos;ll appear here as members pay.</p>
  }

  return (
    <ul className="divide-y divide-border rounded-lg border border-border">
      {recent.data.items.map((c) => (
        // Amount, status, when, which cycle, whose. The id itself is never shown.
        <li key={c.id} className="p-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <span className="font-medium whitespace-nowrap">{formatCents(c.amountCents)}</span>
            <span>{c.status}</span>
            <span className="text-sm text-muted-foreground">
              {c.memberUserId === youId ? 'You' : (names[c.memberUserId] ?? 'A member')} · {formatDateTime(c.recordedAt)} · cycle {c.cycle}
            </span>
          </div>
          <ProofControls
            stokvelId={stokvelId}
            contributionId={c.id}
            hasProof={c.hasProof}
            canDownload={c.canDownloadProof}
            // Your own payment, or any payment where you are the Treasurer; only while its cycle is open.
            canChange={(c.memberUserId === youId || isTreasurer) && c.cycle === openCycleLabel}
          />
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