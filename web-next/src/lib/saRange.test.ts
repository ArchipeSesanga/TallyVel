import { describe, expect, it } from 'vitest'
import { isRealDay, southAfricanDaysToUtcRange } from './saRange'

// These tests must pass whatever time zone the machine is in: `npm run test:tz` runs them with TZ=UTC
// and with TZ=Africa/Johannesburg.

describe('southAfricanDaysToUtcRange', () => {
  it('1 to 31 October starts at 22:00Z on 30 September and ends at 22:00Z on 31 October', () => {
    expect(southAfricanDaysToUtcRange('2026-10-01', '2026-10-31')).toEqual({
      ok: true,
      from: '2026-09-30T22:00:00Z', // included
      to: '2026-10-31T22:00:00Z', // excluded
    })
  })

  it('a contribution at 23:30 on 31 October is inside, and the instant 22:00Z on 31 October is not', () => {
    const range = southAfricanDaysToUtcRange('2026-10-01', '2026-10-31')
    if (!range.ok || range.from === null || range.to === null) throw new Error('expected a range')
    const inside = (instant: string) => instant >= range.from! && instant < range.to! // ISO strings in the same shape sort by time

    expect(inside('2026-10-31T21:30:00Z')).toBe(true) // 23:30 SAST on 31 October
    expect(inside('2026-10-31T22:00:00Z')).toBe(false) // midnight: already 1 November
    expect(inside('2026-09-30T22:00:00Z')).toBe(true) // first moment of 1 October
    expect(inside('2026-09-30T21:59:59Z')).toBe(false) // last second of 30 September
  })

  it('a single day spans exactly 24 hours', () => {
    const range = southAfricanDaysToUtcRange('2026-10-15', '2026-10-15')
    if (!range.ok || range.from === null || range.to === null) throw new Error('expected a range')
    expect(range.from).toBe('2026-10-14T22:00:00Z')
    expect(range.to).toBe('2026-10-15T22:00:00Z')
    expect(Date.parse(range.to) - Date.parse(range.from)).toBe(24 * 60 * 60 * 1000)
  })

  it('the day after the last day rolls over the end of a month and of a year', () => {
    expect(southAfricanDaysToUtcRange(undefined, '2026-12-31')).toEqual({ ok: true, from: null, to: '2026-12-31T22:00:00Z' })
    expect(southAfricanDaysToUtcRange(undefined, '2026-02-28')).toEqual({ ok: true, from: null, to: '2026-02-28T22:00:00Z' })
    expect(southAfricanDaysToUtcRange(undefined, '2024-02-29')).toEqual({ ok: true, from: null, to: '2024-02-29T22:00:00Z' }) // a leap day
  })

  it('from only: everything from the start of that day, with no upper end', () => {
    expect(southAfricanDaysToUtcRange('2026-10-01')).toEqual({ ok: true, from: '2026-09-30T22:00:00Z', to: null })
  })

  it('to only: everything up to the end of that day, with no lower end', () => {
    expect(southAfricanDaysToUtcRange(undefined, '2026-10-31')).toEqual({ ok: true, from: null, to: '2026-10-31T22:00:00Z' })
  })

  it('no dates: no filter at all', () => {
    expect(southAfricanDaysToUtcRange()).toEqual({ ok: true, from: null, to: null })
    expect(southAfricanDaysToUtcRange('', '')).toEqual({ ok: true, from: null, to: null })
  })

  it('to before from is refused, not swapped', () => {
    expect(southAfricanDaysToUtcRange('2026-10-20', '2026-10-10')).toEqual({ ok: false, reason: 'to-before-from' })
  })

  it('2026-02-30 is refused instead of rolling over to 2 March', () => {
    expect(southAfricanDaysToUtcRange('2026-02-30')).toEqual({ ok: false, reason: 'invalid-date' })
    expect(southAfricanDaysToUtcRange(undefined, '2026-02-30')).toEqual({ ok: false, reason: 'invalid-date' })
    expect(isRealDay('2026-02-30')).toBe(false)
  })

  it('anything that is not YYYY-MM-DD is refused', () => {
    for (const text of ['yesterday', '2026-1-5', '2026/10/05', '20261005', '2026-13-01', '2026-00-10', '2026-10-32']) {
      expect(southAfricanDaysToUtcRange(text), text).toEqual({ ok: false, reason: 'invalid-date' })
    }
  })

  it('does not depend on the machine time zone: the same day in a leap year and a non-leap year', () => {
    expect(isRealDay('2024-02-29')).toBe(true)
    expect(isRealDay('2026-02-29')).toBe(false)
  })
})
