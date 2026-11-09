// The rules behind the dashboard's group overview, as plain functions so they can be tested.
// Money is whole cents (see money.ts) and dates are South African calendar days as "YYYY-MM-DD" text, never
// Date objects: a Date at midnight UTC can land on the wrong day in some time zones.

import type { MeHomeEntry } from '@/api/types'
import { formatCents } from './money'

export type Group = {
  id: string
  name: string
  paidThisCycleCents: number
  isPaidUp: boolean
  amountDueCents: number
  /** "YYYY-MM-DD" (South African date) or null when no payout is scheduled. */
  nextPayoutDate: string | null
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

/** "2026-11-09" → "9 Nov". Written by hand: Intl pads the day ("09") and its month names differ between versions. */
export function shortDay(date: string): string {
  const [, month, day] = date.split('-')
  return `${Number(day)} ${MONTHS[Number(month) - 1]}`
}

/** "2026-11-09" → "NOV" (the banner's calendar tile). */
export function monthCaps(date: string): string {
  return MONTHS[Number(date.split('-')[1]) - 1].toUpperCase()
}

/** "2026-11-09" → "9". */
export function dayNumber(date: string): string {
  return String(Number(date.split('-')[2]))
}

/** Two letters for the avatar: "Family Stokvel" → "FS", "Create101" → "C1", "Work" → "WO". */
export function initials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return '?'
  if (words.length > 1) return (words[0][0] + words[1][0]).toUpperCase()
  const rest = words[0].slice(1)
  const digit = rest.match(/\d/)?.[0]
  return (words[0][0] + (digit ?? rest[0] ?? '')).toUpperCase()
}

export type GroupStatus = { text: string; tone: 'paid' | 'due' | 'muted' }

/** The status line under a group's name. "Paid up" is never said about a group that has paid nothing. */
export function statusOf(g: Group): GroupStatus {
  if (g.amountDueCents === 0 && g.paidThisCycleCents === 0) return { text: 'Nothing due this cycle', tone: 'muted' }
  if (g.amountDueCents > 0) return { text: `${formatCents(g.amountDueCents)} due`, tone: 'due' }
  return { text: 'Paid up', tone: 'paid' }
}

/** "2 groups, all paid up" or "1 of 2 groups has money due". */
export function summaryOf(groups: Group[]): string {
  const owing = groups.filter((g) => g.amountDueCents > 0).length
  const n = groups.length
  if (owing === 0) return `${n} ${n === 1 ? 'group' : 'groups'}, all paid up`
  // "1 of 2 groups has money due": the verb agrees with how many owe ("1"), as in the spec's own example.
  return `${owing} of ${n} ${n === 1 ? 'group' : 'groups'} ${owing === 1 ? 'has' : 'have'} money due`
}

/** The group the banner is about: the first whose payout is today, or null (then there is no banner). */
export function groupPayingToday(groups: Group[], today: string): Group | null {
  return groups.find((g) => g.nextPayoutDate === today) ?? null
}

/** The API's home entries → the overview's groups. "Paid up" means something was paid and nothing is still owed. */
export function groupsFromHome(entries: MeHomeEntry[]): Group[] {
  return entries.map((s) => ({
    id: s.stokvelId,
    name: s.name,
    paidThisCycleCents: s.paidThisCycleCents,
    amountDueCents: s.owedThisCycleCents,
    isPaidUp: s.owedThisCycleCents === 0 && s.paidThisCycleCents > 0,
    nextPayoutDate: s.nextPayoutDate,
  }))
}
