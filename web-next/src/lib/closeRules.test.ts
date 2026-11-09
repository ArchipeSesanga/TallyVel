import { describe, expect, it } from 'vitest'
import { chooseCloseKey, closeButtonLabel, closedToastText } from './closeRules'

const KEY = '11111111-1111-4111-8111-111111111111'
const fresh = () => 'fresh-key'

describe('chooseCloseKey', () => {
  it('the same version after an unknown outcome keeps the key (a retry must not close twice)', () => {
    expect(chooseCloseKey(KEY, 'v1', 'v1', fresh)).toBe(KEY)
  })

  it('a key that was never sent can be used as it is', () => {
    expect(chooseCloseKey(KEY, null, 'v1', fresh)).toBe(KEY)
  })

  it('a different version is a different request, so it gets a new key', () => {
    expect(chooseCloseKey(KEY, 'v1', 'v2', fresh)).toBe('fresh-key')
  })

  it('a key that is not shaped like a UUID is replaced', () => {
    expect(chooseCloseKey('', null, 'v1', fresh)).toBe('fresh-key')
    expect(chooseCloseKey('nope', null, 'v1', fresh)).toBe('fresh-key')
  })
})

describe('closeButtonLabel: says what it does, with the API\'s total', () => {
  it.each([
    ['2026-11', 150_000, 'Close 2026-11 and record R 1 500,00 in arrears'],
    ['2026-11', 60_000, 'Close 2026-11 and record R 600,00 in arrears'],
    ['2026-11', 1, 'Close 2026-11 and record R 0,01 in arrears'],
    ['2026-11', 0, 'Close 2026-11 with no arrears'],
  ])('%s with %i cents', (label, cents, expected) => {
    expect(closeButtonLabel(label, cents)).toBe(expected)
  })

  it('is never "OK"', () => {
    expect(closeButtonLabel('2026-11', 0)).not.toMatch(/^ok$/i)
  })
})

describe('closedToastText', () => {
  it('says what was recorded', () => {
    expect(closedToastText('2026-11', 30_000, 1)).toBe('Closed 2026-11. Recorded R 300,00 in arrears for 1 member.')
    expect(closedToastText('2026-11', 60_000, 2)).toBe('Closed 2026-11. Recorded R 600,00 in arrears for 2 members.')
    expect(closedToastText('2026-11', 0, 0)).toBe('Closed 2026-11. No arrears were recorded.')
  })
})
