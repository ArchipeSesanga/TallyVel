import type { ContributionItem, MeHomeEntry, StokvelCurrentCycle, StokvelDetail } from '@/api/types'
import { centsToInput } from './moneyInput'
import { canRunCycle } from './roles'

// What the stokvel page needs, worked out from the two things the API sends (the stokvel and the person's own
// /me/home entry for it). It is a plain function and it takes the GENERATED API types, so if the API renames a field
// the tests that build their data from these types stop compiling.

export type StokvelView = {
  stokvelId: string
  name: string
  /** The signed-in person's role in THIS stokvel. */
  role: MeHomeEntry['role']
  memberCount: number
  members: StokvelDetail['members']
  contributionCents: number
  /** The open cycle, or null (also null for a closed or paid-out one: the page treats it as "no open cycle"). */
  cycle: StokvelCurrentCycle | null
  /** "YYYY-MM-DD" or null. */
  payoutDate: string | null
  /** Treasurer or Chairperson: may record for others, close the cycle, invite. The API checks again on every request. */
  canRun: boolean
  /** What the person still owes, as text they can edit ("150.50"), or "" if nothing. */
  prefill: string
}

export function stokvelViewOf(stokvel: StokvelDetail, entry: MeHomeEntry): StokvelView {
  const open = stokvel.currentCycle !== null && stokvel.currentCycle.status === 'Open' ? stokvel.currentCycle : null
  return {
    stokvelId: stokvel.id,
    name: stokvel.name,
    role: entry.role,
    memberCount: stokvel.memberCount,
    members: stokvel.members,
    contributionCents: stokvel.contributionAmountCents,
    cycle: open,
    payoutDate: entry.nextPayoutDate,
    canRun: canRunCycle(entry.role),
    prefill: entry.owedThisCycleCents > 0 ? centsToInput(entry.owedThisCycleCents) : '',
  }
}

/** Whether to offer attaching, replacing or removing a proof on this contribution: yours, or any if you run the cycle; only while its cycle is open. */
export function mayChangeProof(contribution: Pick<ContributionItem, 'memberUserId' | 'cycle'>, youId: string, canRun: boolean, openCycleLabel: string | null): boolean {
  return (contribution.memberUserId === youId || canRun) && contribution.cycle === openCycleLabel
}

/** The only Chairperson cannot leave or be demoted (the API refuses), so the page says so instead of offering it. */
export function isOnlyChairperson(members: Pick<StokvelDetail['members'][number], 'role'>[], role: MeHomeEntry['role']): boolean {
  return role === 'Chairperson' && members.filter((m) => m.role === 'Chairperson').length === 1
}
