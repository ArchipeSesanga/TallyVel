import { describe, expect, it } from 'vitest'
import { z } from 'zod'
import { contributionSchema } from './contributionSchema'

const valid = {
  stokvelId: 'b12b7944-7790-4aa6-a450-535434d34b05',
  memberUserId: '210ef486-282d-447e-8ad1-a70a5e5c26a5',
  cycle: '2026-11',
  amount: '150.50',
  idempotencyKey: '0b7c1f0e-5f0e-4b43-9d5b-2a1f2f7a9c11',
}

const errorsFor = (input: Record<string, unknown>) => {
  const result = contributionSchema.safeParse(input)
  return result.success ? {} : z.flattenError(result.error).fieldErrors
}

describe('contributionSchema', () => {
  it('accepts a whole, valid form', () => {
    expect(contributionSchema.safeParse(valid).success).toBe(true)
  })

  it('refuses an amount that would need rounding, with one message', () => {
    expect(errorsFor({ ...valid, amount: '150.505' }).amount).toHaveLength(1)
  })

  it('says "more than R 0" only for zero', () => {
    expect(errorsFor({ ...valid, amount: '0.00' }).amount).toEqual(['The amount must be more than R 0,00.'])
  })

  it('refuses text, negatives, zero and an empty amount, each on the amount field', () => {
    for (const amount of ['abc', '-10', '0', '0.00', '']) {
      expect(errorsFor({ ...valid, amount }).amount, `amount "${amount}"`).toBeDefined()
    }
  })

  it('refuses a member id that is not a UUID', () => {
    expect(errorsFor({ ...valid, memberUserId: 'someone' }).memberUserId).toBeDefined()
  })

  it('refuses a missing cycle or key', () => {
    expect(errorsFor({ ...valid, cycle: '  ' }).cycle).toBeDefined()
    expect(errorsFor({ ...valid, idempotencyKey: '' }).idempotencyKey).toBeDefined()
  })
})
