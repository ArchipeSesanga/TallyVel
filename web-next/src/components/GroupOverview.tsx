'use client'

import Link from 'next/link'
import { formatCents } from '@/lib/money'
import {
  dayNumber, groupPayingToday, initials, monthCaps, shortDay, statusOf, summaryOf, type Group,
} from '@/lib/groupOverview'

// One focus style everywhere: a visible 2px ring with an offset (never removed, never animated).
const FOCUS = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-paid focus-visible:ring-offset-2'

type Props = {
  groups: Group[]
  /** Today's South African date, "YYYY-MM-DD". Passed in (the server decides) so the page and the browser never disagree. */
  today: string
  /** TallyVel only tracks payouts, it does not move money: this records the payout, nothing more. */
  onRecordPayout: (groupId: string) => void
  /** Where a group's detail page is. */
  hrefFor: (groupId: string) => string
}

export function GroupOverview({ groups, today, onRecordPayout, hrefFor }: Props) {
  const payingToday = groupPayingToday(groups, today)
  return (
    <div className="flex max-w-[960px] flex-col gap-5 bg-page p-12 text-ink max-sm:p-4">
      {payingToday !== null && <PayoutBanner group={payingToday} today={today} onRecordPayout={onRecordPayout} />}

      <section className="overflow-hidden rounded-2xl border border-line bg-white">
        <div className="flex items-baseline justify-between gap-3 px-7 pt-[22px] pb-3.5 max-sm:px-4">
          <h2 className="text-xl font-semibold">Your groups</h2>
          <p className="text-sm text-ink-muted">{summaryOf(groups)}</p>
        </div>

        {/* Column titles. Decorative: each cell carries its own screen-reader label, and on a phone the rows stack. */}
        <div aria-hidden="true" className="grid grid-cols-[2fr_1fr_1.4fr] gap-4 border-y border-line-soft bg-head px-7 py-2.5 text-[13px] font-medium text-ink-muted max-sm:hidden">
          <span>Group</span>
          <span className="text-right">Paid this cycle</span>
          <span>Next payout</span>
        </div>

        <ul>
          {groups.map((group, index) => (
            <GroupRow key={group.id} group={group} index={index} today={today} href={hrefFor(group.id)} last={index === groups.length - 1} />
          ))}
        </ul>
      </section>
    </div>
  )
}

function PayoutBanner({ group, today, onRecordPayout }: { group: Group; today: string; onRecordPayout: (id: string) => void }) {
  return (
    <section className="flex items-center gap-6 rounded-2xl bg-forest px-7 py-6 text-white max-sm:flex-col max-sm:items-stretch max-sm:px-5">
      {/* A picture of the date: the text says the same thing, so screen readers skip it. */}
      <div aria-hidden="true" className="flex h-[84px] w-[76px] shrink-0 flex-col overflow-hidden rounded-xl bg-white">
        <div className="bg-due-tab py-1 text-center text-[13px] font-bold text-white">{monthCaps(today)}</div>
        <div className="flex flex-1 items-center justify-center text-[32px] leading-none font-bold text-ink tabular-nums">{dayNumber(today)}</div>
      </div>

      <div className="flex flex-1 flex-col gap-1">
        <p className="text-sm text-forest-soft">Payout day</p>
        <h2 className="text-[26px] leading-tight font-bold">{group.name} pays out today</h2>
        <p className="text-[15px] text-forest-text">
          {group.isPaidUp
            ? 'Everyone has paid in for this cycle, so the payout is ready to record.'
            : // Not everyone has paid: do not say they have.
              'Some members still owe for this cycle. Check the amounts before you record the payout.'}
        </p>
      </div>

      <button
        type="button"
        onClick={() => onRecordPayout(group.id)}
        className={`min-h-12 shrink-0 cursor-pointer rounded-xl bg-white px-[22px] text-[15px] font-bold text-forest max-sm:w-full ${FOCUS}`}
      >
        Record payout
      </button>
    </section>
  )
}

const TONE = { paid: 'text-paid', due: 'text-due', muted: 'text-ink-muted' } as const

function GroupRow({ group, index, today, href, last }: { group: Group; index: number; today: string; href: string; last: boolean }) {
  const status = statusOf(group)
  const alternate = index % 2 === 1
  const hasPayout = group.nextPayoutDate !== null
  const rowClass = `grid grid-cols-[2fr_1fr_1.4fr] items-center gap-x-4 gap-y-2 px-7 py-[18px] max-sm:grid-cols-[1fr_auto] max-sm:px-4 ${last ? '' : 'border-b border-line-soft'}`

  const groupCell = (
    <span className="flex gap-3.5 max-sm:col-span-2">
      <span
        aria-hidden="true"
        className={`flex size-11 shrink-0 items-center justify-center rounded-xl text-[15px] font-bold ${alternate ? 'bg-c1-bg text-c1-fg' : 'bg-paid-bg text-paid'}`}
      >
        {initials(group.name)}
      </span>
      <span className="flex min-w-0 flex-col justify-center">
        <span className="text-base font-semibold break-words">{group.name}</span>
        <span className={`text-[13px] font-medium ${TONE[status.tone]}`}>{status.text}</span>
      </span>
    </span>
  )

  const amount = (
    <span className={`text-right text-lg font-semibold whitespace-nowrap tabular-nums max-sm:text-left ${group.paidThisCycleCents === 0 ? 'text-ink-muted' : ''}`}>
      <span className="sr-only">Paid this cycle: </span>
      {formatCents(group.paidThisCycleCents)}
    </span>
  )

  const payout = <PayoutCell date={group.nextPayoutDate} today={today} href={href} />

  // A row with a payout is one link. A row without one has a second link ("Set a date"), and links must not
  // nest, so there only the group cell is a link.
  return (
    <li>
      {hasPayout ? (
        <Link href={href} className={`${rowClass} ${FOCUS}`}>
          {groupCell}
          {amount}
          {payout}
        </Link>
      ) : (
        <div className={rowClass}>
          <Link href={href} className={`rounded-lg max-sm:col-span-2 ${FOCUS}`}>{groupCell}</Link>
          {amount}
          {payout}
        </div>
      )}
    </li>
  )
}

function PayoutCell({ date, today, href }: { date: string | null; today: string; href: string }) {
  const base = 'text-[15px] tabular-nums max-sm:text-right'
  if (date === null) {
    return (
      <span className={`${base} flex flex-wrap items-center gap-x-3 text-ink-muted`}>
        <span><span className="sr-only">Next payout: </span>Not scheduled</span>
        <Link href={href} className={`rounded-lg py-3 text-sm font-semibold text-paid underline-offset-2 hover:underline ${FOCUS}`}>
          Set a date
        </Link>
      </span>
    )
  }
  if (date === today) {
    return (
      <span className={`${base} inline-flex items-center gap-2 font-semibold text-due`}>
        <span aria-hidden="true" className="size-2 rounded-full bg-due-dot" />
        <span><span className="sr-only">Next payout: </span>Today, {shortDay(date)}</span>
      </span>
    )
  }
  return (
    <span className={base}>
      <span className="sr-only">Next payout: </span>
      {shortDay(date)}
    </span>
  )
}
