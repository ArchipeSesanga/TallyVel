import { Suspense } from 'react'
import { CreditCard } from 'lucide-react'
import { notFound } from 'next/navigation'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { formatCents } from '@/lib/money'
import { formatDateTime } from '@/lib/dates'
import { canRunCycle } from '@/lib/roles'
import { centsToInput } from '@/lib/moneyInput'
import { getSession } from '@/lib/session'
import { recordContribution } from '@/app/actions'
import { ContributionForm } from '@/components/ContributionForm'
import { ProofControls } from '@/components/ProofControls'
import { CloseCycleDialog } from '@/components/CloseCycleDialog'
import { FlashToast } from '@/components/FlashToast'
import { Avatar } from '@/components/StokvelParts'
import { StokvelDetail } from '@/components/StokvelDetail'

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

  const { name, contributionAmountCents, memberCount, currentCycle, members } = stokvel.data
  const session = await getSession() // the page is inside <Suspense>, so reading the cookie is fine
  if (session === null) notFound() // readOrFail has already sent a signed-out person to /login; this only narrows the type

  const canRun = canRunCycle(entry.role) // Treasurer or Chairperson, from the API (the API checks again)
  const openCycle = currentCycle !== null && currentCycle.status === 'Open' ? currentCycle : null

  return (
    <StokvelDetail
      stokvelId={id}
      name={name}
      role={entry.role}
      youId={session.user.id}
      memberCount={memberCount}
      members={members}
      contributionCents={contributionAmountCents}
      cycle={openCycle}
      payoutDate={entry.nextPayoutDate}
      canRun={canRun}
      flash={
        <>
          {flags.joined === '1' && <FlashToast message={`You joined ${name}.`} />}
          {flags.created === '1' && <FlashToast message={`Created ${name}. You are its Chairperson.`} />}
        </>
      }
      // Only while the cycle is open. THIS stokvel's role comes from the API (/me/home), never from the session.
      recordForm={
        openCycle === null ? undefined : (
          <ContributionForm
            stokvelId={id}
            cycle={openCycle.label}
            youId={session.user.id}
            members={canRun ? members.map((m) => ({ userId: m.userId, name: m.fullName ?? 'A member' })) : null}
            prefill={entry.owedThisCycleCents > 0 ? centsToInput(entry.owedThisCycleCents) : ''}
            firstKey={crypto.randomUUID()}
            action={recordContribution}
          />
        )
      }
      // Close the cycle: whoever runs THIS stokvel's cycles, and only while it is open.
      closeControl={
        canRun && openCycle !== null ? (
          <CloseCycleDialog stokvelId={id} cycleId={openCycle.id} cycleLabel={openCycle.label} firstKey={crypto.randomUUID()} />
        ) : undefined
      }
      recent={
        <Suspense fallback={<RecentSkeleton />}>
          <RecentContributions
            recentPromise={recentPromise}
            stokvelId={id}
            youId={session.user.id}
            isTreasurer={canRun}
            openCycleLabel={openCycle?.label ?? null}
            names={Object.fromEntries(members.map((m) => [m.userId, m.fullName ?? 'A member']))}
          />
        </Suspense>
      }
    />
  )
}

// Awaits the request that was ALREADY started above, so it has been running all along.
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
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-input px-4 py-7 text-center">
        <CreditCard className="size-7 text-muted-foreground" strokeWidth={1.75} aria-hidden="true" />
        <p className="text-[15px] font-semibold">No contributions yet</p>
        <p className="max-w-[260px] text-sm text-muted-foreground">Payments show up here once a cycle is open and members start paying in.</p>
      </div>
    )
  }

  return (
    <ul className="divide-y divide-divider">
      {recent.data.items.map((c) => {
        const you = c.memberUserId === youId
        const who = you ? 'You' : (names[c.memberUserId] ?? 'A member')
        return (
          // Avatar, name, date and amount. The id itself is never shown.
          <li key={c.id} className="py-3.5 first:pt-0 last:pb-0">
            <div className="flex items-center justify-between gap-3">
              <div className="flex min-w-0 items-center gap-3">
                <Avatar name={who} tone={you ? 'you' : 'alt'} round />
                <div className="flex min-w-0 flex-col">
                  <span className="text-base font-semibold break-words">{who}</span>
                  <span className="text-[13px] text-muted-foreground tabular-nums">
                    {formatDateTime(c.recordedAt)} · {c.status} · cycle {c.cycle}
                  </span>
                </div>
              </div>
              <span className="text-base font-semibold whitespace-nowrap tabular-nums">{formatCents(c.amountCents)}</span>
            </div>
            <ProofControls
              stokvelId={stokvelId}
              contributionId={c.id}
              hasProof={c.hasProof}
              canDownload={c.canDownloadProof}
              // Your own payment, or any payment where you run the cycle; only while its cycle is open.
              canChange={(you || isTreasurer) && c.cycle === openCycleLabel}
            />
          </li>
        )
      })}
    </ul>
  )
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
