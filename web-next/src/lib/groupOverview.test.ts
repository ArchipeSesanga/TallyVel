import { describe, expect, it } from 'vitest'
import { dayNumber, groupPayingToday, groupsFromHome, initials, monthCaps, shortDay, statusOf, summaryOf, type Group } from './groupOverview'

const g = (over: Partial<Group>): Group => ({
  id: 'x', name: 'X', paidThisCycleCents: 0, isPaidUp: false, amountDueCents: 0, nextPayoutDate: null, ...over,
})
const family = g({ id: 'f', name: 'Family Stokvel', paidThisCycleCents: 50000, isPaidUp: true, nextPayoutDate: '2026-11-09' })
const create101 = g({ id: 'c', name: 'Create101' })

describe('dates', () => {
  it('writes the day without padding and the month in short form', () => {
    expect(shortDay('2026-11-09')).toBe('9 Nov')
    expect(shortDay('2026-12-14')).toBe('14 Dec')
    expect(shortDay('2026-09-01')).toBe('1 Sep')
  })
  it('gives the banner tile its month in capitals and its day number', () => {
    expect(monthCaps('2026-11-09')).toBe('NOV')
    expect(dayNumber('2026-11-09')).toBe('9')
  })
})

describe('initials', () => {
  it('uses two words, or a letter and the first digit', () => {
    expect(initials('Family Stokvel')).toBe('FS')
    expect(initials('Create101')).toBe('C1')
    expect(initials('Work')).toBe('WO')
    expect(initials('  ')).toBe('?')
  })
})

describe('statusOf', () => {
  it('says Paid up only when something was paid and nothing is due', () => {
    expect(statusOf(family)).toEqual({ text: 'Paid up', tone: 'paid' })
  })
  it('says Nothing due this cycle, never Paid up, for a group with R 0 and nothing due', () => {
    expect(statusOf(create101)).toEqual({ text: 'Nothing due this cycle', tone: 'muted' })
    expect(statusOf(g({ isPaidUp: true }))).toEqual({ text: 'Nothing due this cycle', tone: 'muted' })
  })
  it('says how much is due in rands', () => {
    expect(statusOf(g({ amountDueCents: 150000 })).text).toBe('R 1 500,00 due')
    expect(statusOf(g({ amountDueCents: 150000 })).tone).toBe('due')
  })
})

describe('summaryOf', () => {
  it('describes a list where nobody owes', () => {
    expect(summaryOf([family, create101])).toBe('2 groups, all paid up')
    expect(summaryOf([family])).toBe('1 group, all paid up')
  })
  it('counts the groups that owe', () => {
    expect(summaryOf([family, g({ amountDueCents: 100 })])).toBe('1 of 2 groups has money due')
    expect(summaryOf([g({ amountDueCents: 100 }), g({ amountDueCents: 5 }), family])).toBe('2 of 3 groups have money due')
  })
})

describe('groupPayingToday', () => {
  it('finds the first group whose payout is today, and none otherwise', () => {
    expect(groupPayingToday([create101, family], '2026-11-09')).toBe(family)
    expect(groupPayingToday([create101, family], '2026-11-10')).toBeNull()
    expect(groupPayingToday([], '2026-11-09')).toBeNull()
  })
  it('takes the first when several pay today', () => {
    const other = g({ id: 'o', nextPayoutDate: '2026-11-09' })
    expect(groupPayingToday([other, family], '2026-11-09')).toBe(other)
  })
})

describe('groupsFromHome', () => {
  const entry = (paid: number, owed: number, next: string | null) => ({
    stokvelId: 'id', name: 'N', role: 'Member' as const, currentCycle: null,
    paidThisCycleCents: paid, owedThisCycleCents: owed, nextPayoutDate: next,
  })
  it('is paid up only when something was paid and nothing is owed', () => {
    expect(groupsFromHome([entry(50000, 0, null)])[0].isPaidUp).toBe(true)
    expect(groupsFromHome([entry(0, 0, null)])[0].isPaidUp).toBe(false)
    expect(groupsFromHome([entry(100, 50, null)])[0].isPaidUp).toBe(false)
  })
  it('keeps the amounts as whole cents and the date as text', () => {
    expect(groupsFromHome([entry(50000, 150000, '2026-11-09')])[0]).toMatchObject({
      paidThisCycleCents: 50000, amountDueCents: 150000, nextPayoutDate: '2026-11-09',
    })
  })
})
