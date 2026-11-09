import { describe, expect, it } from 'vitest'
import { JoinError, joinErrorMessage } from './joinErrors'

describe('joinErrorMessage', () => {
  it('has the agreed words for the three main cases', () => {
    expect(joinErrorMessage(new JoinError('not-found'))).toBe("That code doesn't match any stokvel. Check it and try again.")
    expect(joinErrorMessage(new JoinError('already-member'))).toBe("You're already in this stokvel.")
    expect(joinErrorMessage(new JoinError('unreachable'))).toBe("Couldn't reach TallyVel. Check your connection and try again.")
  })
  it('says how long to wait, and quotes a reference', () => {
    expect(joinErrorMessage(new JoinError('rate-limited', '30'))).toBe('Try again in 30 seconds.')
    expect(joinErrorMessage(new JoinError('failed', 'abc123'))).toContain('Reference: abc123')
    expect(joinErrorMessage(new JoinError('expired'))).toContain('expired')
  })
})
