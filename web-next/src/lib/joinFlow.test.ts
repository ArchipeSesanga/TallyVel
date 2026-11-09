import { describe, expect, it } from 'vitest'
import { CODE_PATTERN, decideStep, inviteLink, scheduleText, stepUrl, type JoinStep, type PreviewKind } from './joinFlow'

const REQUESTED: (JoinStep | null)[] = [null, 'code', 'rules', 'confirm']
const BAD_ANSWERS: PreviewKind[] = ['not-found', 'expired', 'already-member', 'rate-limited', 'failed']

describe('decideStep', () => {
  it('with no code, every request lands on step 1', () => {
    for (const requested of REQUESTED) {
      expect(decideStep(requested, false, null)).toBe('code')
    }
  })

  it('confirm with no valid code goes to step 1', () => {
    expect(decideStep('confirm', false, null)).toBe('code')
    expect(decideStep('confirm', true, 'not-found')).toBe('code')
  })

  it('rules with no code goes to step 1', () => {
    expect(decideStep('rules', false, null)).toBe('code')
  })

  it('the invite link itself (a code, no step) lands on the rules', () => {
    expect(decideStep(null, true, 'valid')).toBe('rules')
  })

  it('step 1 is always allowed with a code (Back goes there), even when the code is fine', () => {
    expect(decideStep('code', true, 'valid')).toBe('code')
    for (const answer of BAD_ANSWERS) expect(decideStep('code', true, answer)).toBe('code')
  })

  it('rules and confirm are allowed with a valid invite', () => {
    expect(decideStep('rules', true, 'valid')).toBe('rules')
    expect(decideStep('confirm', true, 'valid')).toBe('confirm')
  })

  it('every answer other than valid sends steps 2 and 3 back to step 1', () => {
    for (const answer of BAD_ANSWERS) {
      for (const requested of [null, 'rules', 'confirm'] as const) {
        expect(decideStep(requested, true, answer)).toBe('code')
      }
    }
  })

  it('a code that was never checked does not open steps 2 and 3', () => {
    expect(decideStep('rules', true, null)).toBe('code')
    expect(decideStep('confirm', true, null)).toBe('code')
  })
})

describe('stepUrl', () => {
  it('writes the step and the code', () => {
    expect(stepUrl('rules', 'AbC_-123')).toBe('/join?code=AbC_-123&step=rules')
  })

  it('step 1 without a code is /join?step=code', () => {
    expect(stepUrl('code', null)).toBe('/join?step=code')
  })

  it('escapes anything odd in a code', () => {
    expect(stepUrl('code', 'a b&c')).toBe('/join?code=a+b%26c&step=code')
  })

  it('is its own fixed point: the step in the address is the step shown', () => {
    expect(decideStep('rules', true, 'valid')).toBe('rules')
    expect(decideStep('code', false, null)).toBe('code')
  })
})

describe('inviteLink', () => {
  it('is the full address of the rules step', () => {
    expect(inviteLink('https://tallyvel.example', 'AbC_-123')).toBe('https://tallyvel.example/join?code=AbC_-123&step=rules')
  })
})

describe('scheduleText', () => {
  it('names the frequency and adds the days only when there is a number', () => {
    expect(scheduleText('Weekly', 7)).toBe('Weekly (every 7 days)')
    expect(scheduleText('BiWeekly', 14)).toBe('Every two weeks (every 14 days)')
    expect(scheduleText('Monthly', null)).toBe('Monthly')
  })

  it('never writes "null"', () => {
    expect(scheduleText('Monthly', null)).not.toMatch(/null/)
  })
})

describe('CODE_PATTERN', () => {
  it('accepts a real 22-character code and refuses odd characters', () => {
    expect(CODE_PATTERN.test('-7R8qvcwyFW3lqWL8Cc2Ow')).toBe(true)
    expect(CODE_PATTERN.test('abc def')).toBe(false)
    expect(CODE_PATTERN.test('abc/../x')).toBe(false)
    expect(CODE_PATTERN.test('')).toBe(false)
    expect(CODE_PATTERN.test('a'.repeat(65))).toBe(false)
  })
})
