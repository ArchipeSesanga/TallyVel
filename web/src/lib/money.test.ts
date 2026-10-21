import { describe, expect, it } from 'vitest'
import { formatCents } from './money'

// A no-break space (U+00A0). It looks like a normal space, but it isn't one.
const NBSP = '\u00A0'

describe('formatCents', () => {
  it('formats 10 cents', () => {
    expect(formatCents(10)).toBe(`R${NBSP}0,10`)
  })

  it('formats zero', () => {
    expect(formatCents(0)).toBe(`R${NBSP}0,00`)
  })

  it('formats rands and cents', () => {
    expect(formatCents(15050)).toBe(`R${NBSP}150,50`)
  })

  it('groups thousands with a no-break space', () => {
    expect(formatCents(1234567)).toBe(`R${NBSP}12${NBSP}345,67`)
  })

  it('throws for a fraction of a cent', () => {
    expect(() => formatCents(10.5)).toThrow('Not a whole number of cents')
  })

  // Our own edge case: a large stokvel total
  it('formats millions exactly', () => {
    expect(formatCents(999999999)).toBe(`R${NBSP}9${NBSP}999${NBSP}999,99`)
  })
})