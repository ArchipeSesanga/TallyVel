import { describe, expect, it } from 'vitest'
import { southAfricanDaysToUtcRange } from './saRange'

// Which month does a payment belong to? The one it was made in IN SOUTH AFRICA, not in UTC. 23:30 on the last day
// of a month in Johannesburg is 21:30 UTC the same day, so it is safe either way; but 00:30 on the 1st in Johannesburg
// is 22:30 UTC on the LAST day of the month before, and a UTC-based month would put it in the wrong month.
// Instants are written with an explicit offset (+02:00), so the test means the same thing on any machine.

const inMonth = (instant: string, first: string, last: string) => {
  const range = southAfricanDaysToUtcRange(first, last)
  if (!range.ok || range.from === null || range.to === null) throw new Error('bad range in the test')
  const at = new Date(instant).getTime()
  return at >= Date.parse(range.from) && at < Date.parse(range.to)
}

describe('a payment belongs to its South African month', () => {
  it.each([
    // [first day, last day, 23:30 SAST on the last day, 00:30 SAST on the first day of the next month]
    ['2026-01-01', '2026-01-31', '2026-01-31T23:30:00+02:00', '2026-02-01T00:30:00+02:00'],
    ['2026-02-01', '2026-02-28', '2026-02-28T23:30:00+02:00', '2026-03-01T00:30:00+02:00'], // not a leap year
    ['2028-02-01', '2028-02-29', '2028-02-29T23:30:00+02:00', '2028-03-01T00:30:00+02:00'], // a leap year
    ['2026-10-01', '2026-10-31', '2026-10-31T23:30:00+02:00', '2026-11-01T00:30:00+02:00'],
    ['2026-11-01', '2026-11-30', '2026-11-30T23:30:00+02:00', '2026-12-01T00:30:00+02:00'],
    ['2026-12-01', '2026-12-31', '2026-12-31T23:30:00+02:00', '2027-01-01T00:30:00+02:00'], // across New Year
  ])('%s to %s', (first, last, lateOnLastDay, earlyNextMonth) => {
    expect(inMonth(lateOnLastDay, first, last)).toBe(true)
    expect(inMonth(earlyNextMonth, first, last)).toBe(false)
  })

  it('23:30 on 31 October in Johannesburg is 21:30 UTC, and still October', () => {
    expect(new Date('2026-10-31T23:30:00+02:00').toISOString()).toBe('2026-10-31T21:30:00.000Z')
    expect(inMonth('2026-10-31T23:30:00+02:00', '2026-10-01', '2026-10-31')).toBe(true)
  })

  it('00:30 on 1 November in Johannesburg is 22:30 UTC on 31 October, and already November', () => {
    expect(new Date('2026-11-01T00:30:00+02:00').toISOString()).toBe('2026-10-31T22:30:00.000Z')
    expect(inMonth('2026-11-01T00:30:00+02:00', '2026-10-01', '2026-10-31')).toBe(false)
    expect(inMonth('2026-11-01T00:30:00+02:00', '2026-11-01', '2026-11-30')).toBe(true)
  })
})
