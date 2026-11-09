import Link from 'next/link'
import { ChevronLeft } from 'lucide-react'
import { Avatar, CARD, FactsStrip, MembersCard, SetupCard } from '@/components/StokvelParts'
import { LazyCloseCycleDialog } from '@/components/LazyCloseCycleDialog'
import { ContributionForm } from '@/components/ContributionForm'
import { LazyLeaveStokvelDialog } from '@/components/LazyLeaveStokvelDialog'
import { PinToggle } from '@/components/PinToggle'
import { roleLabel } from '@/lib/roles'
import { isOnlyChairperson, type StokvelView } from '@/lib/stokvelView'

const LINK = 'rounded-lg font-semibold text-success focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none'

type Props = {
  /** Everything about the stokvel and the person's place in it (see lib/stokvelView.ts). */
  view: StokvelView
  youId: string
  /** The Server Action the contribution form sends (the page passes the real one). */
  recordAction: React.ComponentProps<typeof ContributionForm>['action']
  /** First Idempotency-Keys. The SERVER makes them, so the page and the browser agree on them. */
  recordKey: string
  closeKey: string
  /** Toasts after joining or creating. */
  flash?: React.ReactNode
  /** The recent-contributions body (list or empty state), drawn by the page because it waits for the API. */
  recent: React.ReactNode
}

/**
 * The stokvel page's layout. It arranges things and shows or hides the actions the person's role allows; every form
 * and action (record, close, invite, roles, leave, pin) is the existing component. h1 keeps id="stokvel-heading".
 * Hiding is only about not offering what would be refused: the API checks the role again on every request.
 */
export function StokvelDetail({ view, youId, recordAction, recordKey, closeKey, flash, recent }: Props) {
  const { stokvelId, name, role, memberCount, members, contributionCents, cycle, payoutDate, canRun, prefill } = view
  const isChair = role === 'Chairperson'
  const onlyChair = isOnlyChairperson(members, role)
  const showSetup = cycle === null || memberCount === 1

  return (
    <div className="flex w-full max-w-[960px] flex-col gap-6 bg-background px-12 pt-10 pb-12 max-sm:px-4 max-sm:pt-6 max-sm:pb-8">
      {flash}

      <Link href="/stokvels" className={`inline-flex items-center gap-1 self-start py-1.5 text-sm ${LINK}`}>
        <ChevronLeft className="size-4" aria-hidden="true" />
        My stokvels
      </Link>

      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex min-w-0 items-center gap-[18px]">
          <Avatar name={name} tone="alt" large />
          <div className="flex min-w-0 flex-col gap-1.5">
            <h1 id="stokvel-heading" tabIndex={-1} className="text-[32px] leading-tight font-bold tracking-[-0.02em] break-words outline-none">{name}</h1>
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="rounded-full bg-success-soft px-2.5 py-[3px] text-[13px] font-semibold text-success">{roleLabel(role)}</span>
              <span className="text-sm text-muted-foreground tabular-nums">{memberCount === 1 ? '1 member' : `${memberCount} members`}</span>
            </div>
          </div>
        </div>
        <PinToggle stokvelId={stokvelId} name={name} />
      </header>

      <FactsStrip contributionCents={contributionCents} memberCount={memberCount} cycle={cycle} payoutDate={payoutDate} />

      {showSetup && (
        <SetupCard stokvelId={stokvelId} name={name} contributionCents={contributionCents} memberCount={memberCount} openCycleLabel={cycle?.label ?? null} canRun={canRun} />
      )}

      {/* Only while the cycle is open. */}
      {cycle !== null && (
        <section aria-labelledby="record-heading" className={`${CARD} flex flex-col gap-3.5 px-6 py-[22px]`}>
          <h2 id="record-heading" className="text-xl font-semibold">Record a contribution</h2>
          <ContributionForm
            stokvelId={stokvelId}
            cycle={cycle.label}
            youId={youId}
            // Whoever runs the cycle may record for anyone; a member can only record for themselves.
            members={canRun ? members.map((m) => ({ userId: m.userId, name: m.fullName ?? 'A member' })) : null}
            prefill={prefill}
            firstKey={recordKey}
            action={recordAction}
          />
        </section>
      )}

      {/* Close the cycle: whoever runs THIS stokvel's cycles, and only while it is open. */}
      {canRun && cycle !== null && (
        <section aria-labelledby="close-heading" className={`${CARD} flex flex-wrap items-center justify-between gap-4 px-6 py-5`}>
          <h2 id="close-heading" className="text-base font-semibold">Close the cycle</h2>
          <LazyCloseCycleDialog stokvelId={stokvelId} cycleId={cycle.id} cycleLabel={cycle.label} firstKey={closeKey} />
        </section>
      )}

      <div className="grid grid-cols-[1.15fr_1fr] items-start gap-5 max-md:grid-cols-1">
        <MembersCard stokvelId={stokvelId} members={members} youId={youId} isChair={isChair} showInvite={canRun && !showSetup} />

        <section aria-labelledby="recent-heading" className={`${CARD} flex flex-col gap-[18px] px-6 py-[22px]`}>
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 id="recent-heading" className="text-xl font-semibold">Recent contributions</h2>
            <Link href={`/stokvels/${stokvelId}/contributions`} className={`inline-flex min-h-6 items-center text-sm ${LINK}`}>Full history</Link>
          </div>
          {recent}
        </section>
      </div>

      <section aria-labelledby="leave-heading" className={`${CARD} flex flex-wrap items-center justify-between gap-4 px-6 py-5`}>
        <div className="flex max-w-[560px] flex-col gap-1">
          <h2 id="leave-heading" className="text-base font-semibold">Leave {name}</h2>
          <p id="leave-reason" className="text-sm text-muted-foreground">
            {onlyChair
              ? "You're the only Chairperson, so you can't leave yet. Make another member Chairperson first."
              : "You'll stop seeing this stokvel and its contributions."}
          </p>
        </div>
        <LazyLeaveStokvelDialog stokvelId={stokvelId} stokvelName={name} blocked={onlyChair} />
      </section>
    </div>
  )
}
