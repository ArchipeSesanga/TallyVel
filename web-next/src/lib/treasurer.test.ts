import { describe, expect, it } from 'vitest'
import type { MeHomeEntry } from '@/api/types'
import { stokvelsWhereTreasurer } from './treasurer'

function entry(stokvelId: string, role: MeHomeEntry['role']): MeHomeEntry {
  return {
    stokvelId,
    name: `Stokvel ${stokvelId}`,
    role,
    currentCycle: null,
    paidThisCycleCents: 0,
    owedThisCycleCents: 0,
    nextPayoutDate: null,
  }
}

describe('stokvelsWhereTreasurer', () => {
  it('keeps only the stokvels where the person is Treasurer', () => {
    const treasurer = entry('a', 'Treasurer')
    const result = stokvelsWhereTreasurer([treasurer, entry('b', 'Member'), entry('c', 'Chairperson')])
    expect(result).toEqual([treasurer])
  })

  it('returns an empty list when the person is Treasurer nowhere', () => {
    expect(stokvelsWhereTreasurer([entry('a', 'Member'), entry('b', 'Chairperson')])).toEqual([])
  })

  it('returns an empty list for an empty list', () => {
    expect(stokvelsWhereTreasurer([])).toEqual([])
  })
})
