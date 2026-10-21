import { describe, expect, it } from 'vitest'
import { canRunCycle, roleLabel } from './roles'

describe('canRunCycle', () => {
  it('lets a Treasurer and a Chairperson run a cycle', () => {
    expect(canRunCycle('Treasurer')).toBe(true)
    expect(canRunCycle('Chairperson')).toBe(true)
  })

  it('does not let a Member run a cycle', () => {
    expect(canRunCycle('Member')).toBe(false)
  })
})

describe('roleLabel', () => {
  it('words each role for the person reading', () => {
    expect(roleLabel('Treasurer')).toBe("You're the Treasurer")
    expect(roleLabel('Member')).toBe("You're a Member")
    expect(roleLabel('Chairperson')).toBe("You're the Chairperson")
  })
})
