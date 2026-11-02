import { describe, expect, it } from 'vitest'
import { formatDate, formatDateTime } from './dates'

// Lists show South African time whatever the machine's time zone is (`npm run test:tz` checks both).

describe('formatDateTime', () => {
  it('shows a contribution at 23:30 on 31 October as 31 October, not 1 November', () => {
    const text = formatDateTime('2026-10-31T21:30:00Z')
    expect(text).toContain('31')
    expect(text).toContain('23:30')
    expect(text).not.toMatch(/\b0?1 Nov/)
  })

  it('shows midnight on 1 November (22:00Z the evening before) as 1 November', () => {
    expect(formatDateTime('2026-10-31T22:00:00Z')).toMatch(/0?1 Nov/)
  })
})

describe('formatDate', () => {
  it('shows a calendar date from the API on the same day', () => {
    expect(formatDate('2026-10-31')).toContain('31')
    expect(formatDate('2026-10-31')).toContain('Oct')
  })
})
