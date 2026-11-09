import { Check } from 'lucide-react'
import { CreateInviteLink } from '@/components/CreateInviteLink'
import { MemberRoleForm } from '@/components/MemberRoleForm'
import { Button } from '@/components/ui/button'
import { formatCents } from '@/lib/money'
import { formatDate } from '@/lib/dates'
import { initials } from '@/lib/groupOverview'
import { roleWord } from '@/lib/roles'
import type { MemberRole } from '@/api/types'

/** The look every card on the stokvel page shares: white, 1px border, 16px corners, no shadow. */
export const CARD = 'overflow-hidden rounded-2xl border border-border bg-card'

type Tone = 'you' | 'alt'
const TONE: Record<Tone, string> = { you: 'bg-success-soft text-success', alt: 'bg-avatar-alt-bg text-avatar-alt-fg' }

/** Initials in a coloured tile. Decorative: the name is always written next to it. */
export function Avatar({ name, tone, large = false, round = false }: { name: string; tone: Tone; large?: boolean; round?: boolean }) {
  return (
    <span
      aria-hidden="true"
      className={`flex shrink-0 items-center justify-center font-bold ${TONE[tone]} ${large ? 'size-16 rounded-2xl text-[22px]' : 'size-10 text-sm'} ${round ? 'rounded-full' : 'rounded-xl'}`}
    >
      {initials(name)}
    </span>
  )
}

type OpenCycle = { label: string; collectedCents: number; dueCents: number } | null

/** Contribution, member count and the open cycle, in one card of three cells (stacked on a phone). */
export function FactsStrip({
  contributionCents, memberCount, cycle, payoutDate,
}: { contributionCents: number; memberCount: number; cycle: OpenCycle; payoutDate: string | null }) {
  const cell = 'flex flex-col gap-1.5 border-r border-divider px-6 py-5 last:border-r-0 max-sm:border-r-0 max-sm:border-b max-sm:last:border-b-0'
  return (
    <dl className={`grid grid-cols-3 max-sm:grid-cols-1 ${CARD}`}>
      <div className={cell}>
        <dt className="text-sm text-muted-foreground">Contribution per cycle</dt>
        <dd className="text-[26px] leading-tight font-bold whitespace-nowrap tabular-nums">{formatCents(contributionCents)}</dd>
      </div>
      <div className={cell}>
        <dt className="text-sm text-muted-foreground">Members</dt>
        {/* The API already counted the members; the page never counts or adds anything up. */}
        <dd className="text-[26px] leading-tight font-bold tabular-nums">{memberCount}</dd>
      </div>
      <div className={cell}>
        <dt className="text-sm text-muted-foreground">Current cycle</dt>
        {cycle === null ? (
          <dd className="pt-1.5 text-lg font-semibold text-muted-foreground">No open cycle</dd>
        ) : (
          <dd className="flex flex-col gap-1">
            <span className="text-lg font-semibold tabular-nums">{payoutDate !== null ? `Payout ${formatDate(payoutDate)}` : `Cycle ${cycle.label}`}</span>
            <span className="text-sm text-muted-foreground tabular-nums">
              {formatCents(cycle.collectedCents)} collected of {formatCents(cycle.dueCents)}
            </span>
          </dd>
        )}
      </div>
    </dl>
  )
}

type StepState = 'done' | 'current' | 'later'

function Step({
  n, state, title, description, children,
}: { n: number; state: StepState; title: string; description: string; children: React.ReactNode }) {
  const circle =
    state === 'later' ? 'bg-background text-muted-foreground' : 'bg-success-soft text-success' // current and done are green
  return (
    <li className="flex flex-col gap-3.5 rounded-xl bg-card px-5 py-[18px] text-foreground">
      <div className="flex gap-3">
        <span aria-hidden="true" className={`flex size-7 shrink-0 items-center justify-center rounded-full text-sm font-bold ${circle}`}>
          {state === 'done' ? <Check className="size-4" strokeWidth={3} /> : n}
        </span>
        <div className="flex flex-col gap-0.5">
          <h3 className="text-base font-semibold">{title}</h3>
          <p className="text-sm text-muted-foreground">{description}</p>
        </div>
      </div>
      {children}
    </li>
  )
}

/** Shown while there is no open cycle or the stokvel has only one member: what to do next. Buttons are for people who run the cycle. */
export function SetupCard({
  stokvelId, name, contributionCents, memberCount, openCycleLabel, canRun,
}: { stokvelId: string; name: string; contributionCents: number; memberCount: number; openCycleLabel: string | null; canRun: boolean }) {
  const invited = memberCount > 1
  const opened = openCycleLabel !== null
  const state1: StepState = invited ? 'done' : 'current'
  const state2: StepState = opened ? 'done' : invited ? 'current' : 'later'
  return (
    <section aria-labelledby="setup-heading" className="flex flex-col gap-[18px] rounded-2xl bg-primary px-7 py-[26px] text-white max-sm:px-5">
      <div className="flex flex-col gap-1">
        <h2 id="setup-heading" className="text-[22px] font-bold">Get {name} going</h2>
        <p className="text-[15px] text-forest-text">Two steps before anyone can start paying in.</p>
      </div>
      <ol className="grid list-none grid-cols-2 gap-3.5 max-sm:grid-cols-1">
        <Step n={1} state={state1} title="Invite members"
          description={invited ? `${memberCount} members so far` : 'Share a link on WhatsApp or SMS. Anyone with it can join.'}>
          {canRun ? <CreateInviteLink stokvelId={stokvelId} variant={invited ? 'outline' : 'default'} /> : <p className="text-sm font-medium text-muted-foreground">{invited ? 'Done' : 'Not done yet'}</p>}
        </Step>
        <Step n={2} state={state2} title="Open the first cycle"
          description={opened ? `Cycle ${openCycleLabel} is open.` : `Set the payout date so members know when ${formatCents(contributionCents)} is due.`}>
          {canRun ? (
            <div className="flex flex-col items-start gap-1.5">
              {/* TODO: wire this to an "open a cycle" action. The API has POST /stokvels/{id}/cycles, but the app has no
                  action for it yet, so the button stays disabled and says so. */}
              <Button type="button" variant="outline" disabled>Open a cycle</Button>
              {!opened && <p className="text-[13px] text-muted-foreground">Opening a cycle isn&apos;t available in the app yet.</p>}
            </div>
          ) : (
            <p className="text-sm font-medium text-muted-foreground">{opened ? 'Done' : 'Not done yet'}</p>
          )}
        </Step>
      </ol>
    </section>
  )
}

type Member = { userId: string; fullName?: string | null; role: MemberRole }

export const CHAIR_RULE = 'As Chairperson you choose the Treasurer. A stokvel always keeps at least one Chairperson.'

/** Everyone in the stokvel and their role. A Chairperson also gets the role pickers. */
export function MembersCard({
  stokvelId, members, youId, isChair, showInvite,
}: { stokvelId: string; members: Member[]; youId: string; isChair: boolean; showInvite: boolean }) {
  const chairs = members.filter((m) => m.role === 'Chairperson').length
  const count = members.length === 1 ? 'Only you so far' : `${members.length} members`
  return (
    <section aria-labelledby="members-heading" className={CARD}>
      <div className="flex flex-wrap items-center justify-between gap-3 px-6 pt-[22px] pb-3.5">
        <h2 id="members-heading" className="text-xl font-semibold">Members</h2>
        <div className="flex flex-wrap items-center gap-3">
          <span className="text-sm text-muted-foreground tabular-nums">{count}</span>
          {showInvite && <CreateInviteLink stokvelId={stokvelId} variant="outline" label="Invite" />}
        </div>
      </div>
      <ul className="divide-y divide-divider border-t border-divider">
        {members.map((m) => {
          const name = m.fullName ?? 'A member'
          const you = m.userId === youId
          const onlyChair = m.role === 'Chairperson' && chairs === 1
          return (
            <li key={m.userId} className="flex flex-wrap items-center justify-between gap-3 px-6 py-4">
              <div className="flex min-w-0 items-center gap-3">
                <Avatar name={name} tone={you ? 'you' : 'alt'} round />
                <div className="flex min-w-0 flex-col">
                  <span className="text-base font-semibold break-words">
                    {name}{you && <span className="font-medium text-muted-foreground"> (you)</span>}
                  </span>
                  <span className="text-[13px] text-muted-foreground">{roleWord(m.role)}</span>
                </div>
              </div>
              {isChair && (
                <MemberRoleForm stokvelId={stokvelId} userId={m.userId} name={name} role={m.role} lockedByRuleId={onlyChair ? 'chair-rule' : undefined} />
              )}
            </li>
          )
        })}
      </ul>
      {isChair && (
        <p id="chair-rule" className="border-t border-divider bg-muted px-6 pt-3.5 pb-5 text-sm text-muted-foreground">{CHAIR_RULE}</p>
      )}
    </section>
  )
}
