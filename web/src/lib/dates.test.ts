import { describe, expect, it } from 'vitest'
import { formatDate, formatDateTime } from './dates'

describe('formatDateTime', () => {
  it('shows a payment at 00:30 on 1 November in Johannesburg, not 31 October', () => {
    expect(formatDateTime('2026-10-31T22:30:00Z')).toBe('01 Nov 2026, 00:30')
  })
})

describe('formatDate', () => {
  it('keeps a South African calendar date on the same day', () => {
    expect(formatDate('2026-10-31')).toBe('31 Oct 2026')
  })
})