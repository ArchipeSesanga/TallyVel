import { describe, expect, it } from 'vitest'
import { centsToInput, parseRandsToCents } from './moneyInput'

describe('parseRandsToCents', () => {
  it('reads rands and cents', () => {
    expect(parseRandsToCents('150.50')).toBe(15050)
  })

  it('reads a single cents digit as tens of cents', () => {
    expect(parseRandsToCents('150.5')).toBe(15050)
  })

  it('reads 19.99 exactly (a classic floating-point trap)', () => {
    expect(parseRandsToCents('19.99')).toBe(1999)
  })

  it('reads whole rands', () => {
    expect(parseRandsToCents('150')).toBe(15000)
  })

  it('accepts a South African comma and spaces', () => {
    expect(parseRandsToCents('1 500,50')).toBe(150050)
  })

  it('refuses anything that would need rounding', () => {
    expect(parseRandsToCents('150.505')).toBeNull()
  })

  it('refuses text, negatives and empty input', () => {
    expect(parseRandsToCents('abc')).toBeNull()
    expect(parseRandsToCents('-10')).toBeNull()
    expect(parseRandsToCents('')).toBeNull()
  })
})

describe('centsToInput', () => {
  it('writes cents as editable text', () => {
    expect(centsToInput(15050)).toBe('150.50')
    expect(centsToInput(5)).toBe('0.05')
  })
})

describe('round trip', () => {
  it('reads back exactly what it prefilled, for every amount from 0 to 100 000 cents', () => {
    for (let cents = 0; cents <= 100_000; cents++) {
      expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
    }
  })
})