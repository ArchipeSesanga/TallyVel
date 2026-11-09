import { describe, expect, it } from 'vitest'
import { formatCents } from './money'
import { MAX_CONTRIBUTION_CENTS, centsToInput, parseRandsToCents } from './moneyInput'

// One line per case: what was typed, and the exact whole number of cents it must give (or null: refused).
// There are no rands-as-fractions in this file: every expectation is an integer or null.

describe('parseRandsToCents: accepts', () => {
  it.each([
    ['1500', 150_000],
    ['1 500', 150_000],
    ['1 500,50', 150_050],
    ['R1500', 150_000],
    ['R 1 500,50', 150_050],
    ['1 500', 150_000], // a no-break space between the thousands, which is what formatCents writes
    ['R 1 500,50', 150_050], // exactly what formatCents(150050) writes
    ['1500,5', 150_050], // one decimal place is tens of cents
    [',5', 50],
    ['19,99', 1999], // the classic floating-point trap: 19.99 * 100 is 1998.9999999999998
    ['19.99', 1999],
    ['150', 15_000],
    ['150.5', 15_050],
    ['150.50', 15_050],
    ['0', 0],
    ['0,01', 1],
    ['  250,00  ', 25_000], // spaces around it are just spaces
    ['r250', 25_000],
    ['90071992547409,91', MAX_CONTRIBUTION_CENTS], // the API's maximum, to the cent
  ])('%j → %i cents', (typed, cents) => {
    expect(parseRandsToCents(typed)).toBe(cents)
  })
})

describe('parseRandsToCents: refuses (null, and never rounds)', () => {
  it.each([
    ['1500,505'], // three decimal places
    ['150.505'],
    ['1,500.50'], // ambiguous: thousands comma or decimal comma?
    ['1.500,50'],
    ['1,500'], // three digits after a comma read as three decimals, which is refused, not guessed
    ['abc'],
    ['15a'],
    ['1e3'],
    ['R'],
    ['R '],
    ['R R10'],
    ['10 R'],
    [''],
    ['   '],
    ['-10'],
    ['-0,50'],
    ['+10'],
    ['1 5000'], // a space that is not between groups of three
    ['15 00'],
    ['90071992547409,92'], // one cent above the API's maximum
    ['99999999999999999999'],
    [',,5'],
    ['.'],
  ])('%j → null', (typed) => {
    expect(parseRandsToCents(typed)).toBeNull()
  })
})

describe('centsToInput', () => {
  it.each([
    [0, '0.00'],
    [5, '0.05'],
    [99, '0.99'],
    [100, '1.00'],
    [15_050, '150.50'],
    [MAX_CONTRIBUTION_CENTS, '90071992547409.91'],
  ])('%i cents → %j', (cents, text) => {
    expect(centsToInput(cents)).toBe(text)
  })

  it('refuses anything that is not a whole, non-negative number of cents', () => {
    expect(() => centsToInput(1.5)).toThrow()
    expect(() => centsToInput(-1)).toThrow()
    expect(() => centsToInput(Number.NaN)).toThrow()
  })
})

describe('round trips', () => {
  // format → parse must give the same cents back (formatCents writes "R 1 500,50" with no-break spaces).
  it.each([0, 1, 99, 100, 150_050, MAX_CONTRIBUTION_CENTS])('format, then parse, gives %i back', (cents) => {
    expect(parseRandsToCents(formatCents(cents))).toBe(cents)
  })

  it.each([0, 1, 99, 100, 150_050, MAX_CONTRIBUTION_CENTS])('edit text, then parse, gives %i back', (cents) => {
    expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
  })

  it('reads back exactly what it prefilled, for every amount from 0 to 100 000 cents', () => {
    for (let cents = 0; cents <= 100_000; cents++) {
      expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
    }
  })

  it('stays equal to the API\'s limit (Money.MaxCents is 2^53 − 1)', () => {
    expect(MAX_CONTRIBUTION_CENTS).toBe(Number.MAX_SAFE_INTEGER)
  })
})
