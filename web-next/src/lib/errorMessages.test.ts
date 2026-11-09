import { describe, expect, it } from 'vitest'
import { tryAgainIn } from './errorMessages'

describe('tryAgainIn', () => {
  it('uses the seconds from Retry-After', () => {
    expect(tryAgainIn('60')).toBe('Try again in 60 seconds.')
    expect(tryAgainIn('4.2')).toBe('Try again in 5 seconds.')
  })

  it('has a fallback for a missing or odd header', () => {
    expect(tryAgainIn(null)).toBe('Try again in a moment.')
    expect(tryAgainIn('soon')).toBe('Try again in a moment.')
    expect(tryAgainIn('0')).toBe('Try again in a moment.')
  })
})
