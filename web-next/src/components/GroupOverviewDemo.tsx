'use client'

import { useState } from 'react'
import { GroupOverview } from '@/components/GroupOverview'
import type { Group } from '@/lib/groupOverview'

// Fixed data, so the demo looks the same on any day. "Today" is the example's date.
// The first two groups are the spec's example; the third shows an amount due and a future payout.
const TODAY = '2026-11-09'
const GROUPS: Group[] = [
  { id: 'family', name: 'Family Stokvel', paidThisCycleCents: 50000, isPaidUp: true, amountDueCents: 0, nextPayoutDate: '2026-11-09' },
  { id: 'create101', name: 'Create101', paidThisCycleCents: 0, isPaidUp: false, amountDueCents: 0, nextPayoutDate: null },
  { id: 'work', name: 'Work Stokvel', paidThisCycleCents: 20000, isPaidUp: false, amountDueCents: 150000, nextPayoutDate: '2026-12-14' },
]

/** A demo of every state of the group overview. The button only says what it would do; nothing is saved. */
export function GroupOverviewDemo() {
  const [last, setLast] = useState<string | null>(null)
  return (
    <div>
      <GroupOverview
        groups={GROUPS}
        today={TODAY}
        hrefFor={(id) => `/demo/group-overview#${id}`}
        onRecordPayout={(id) => setLast(`Would record the payout for "${GROUPS.find((g) => g.id === id)?.name}". (Demo: nothing was saved.)`)}
      />
      <p role="status" className="px-12 text-sm text-ink-muted max-sm:px-4">{last}</p>
    </div>
  )
}
