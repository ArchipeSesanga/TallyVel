import { describe, expect, it } from 'vitest'
import { createStokvelSchema, NAME_MAX, RULES_MAX } from './stokvelSchema'

const good = { name: 'Family Stokvel', amount: '500', frequency: 'Monthly', rules: '' }
const errorsOf = (input: object): string[] => {
  const r = createStokvelSchema.safeParse(input)
  return r.success ? [] : Object.keys(r.error.flatten().fieldErrors)
}

describe('createStokvelSchema', () => {
  it('accepts a normal stokvel, with or without rules', () => {
    expect(createStokvelSchema.safeParse(good).success).toBe(true)
    expect(createStokvelSchema.safeParse({ ...good, rules: 'Pay on the 5th.' }).success).toBe(true)
  })

  it('needs a name, and trims it', () => {
    expect(errorsOf({ ...good, name: '   ' })).toEqual(['name'])
    expect(errorsOf({ ...good, name: 'a'.repeat(NAME_MAX + 1) })).toEqual(['name'])
    expect(createStokvelSchema.parse({ ...good, name: '  Club ' }).name).toBe('Club')
  })

  it('needs a positive amount in rands and cents, never a fraction of a cent', () => {
    for (const amount of ['', '0', '0,00', '-5', 'abc', '10.505']) expect(errorsOf({ ...good, amount })).toEqual(['amount'])
    for (const amount of ['500', '500,50', '1 500.5']) expect(errorsOf({ ...good, amount })).toEqual([])
  })

  it('only takes the three frequencies', () => {
    expect(errorsOf({ ...good, frequency: 'Daily' })).toEqual(['frequency'])
    expect(errorsOf({ ...good, frequency: '' })).toEqual(['frequency'])
  })

  it('limits the rules', () => {
    expect(errorsOf({ ...good, rules: 'x'.repeat(RULES_MAX + 1) })).toEqual(['rules'])
  })
})
