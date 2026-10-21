import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import type { MeHomeEntry } from '../api/types'
import { home } from '../data/sample'
import { StokvelCard } from './StokvelCard'

// Lerato's two real entries, captured from the API.
const treasurerEntry = home.find((e) => e.role === 'Treasurer')!
const memberEntry = home.find((e) => e.role === 'Member')!

function render(entry: MeHomeEntry): string {
  return renderToStaticMarkup(<StokvelCard entry={entry} />)
}

describe('StokvelCard role handling', () => {
  it('shows the Treasurer tasks to a Treasurer', () => {
    const html = render(treasurerEntry)
    expect(html).toContain('Treasurer tasks')
    expect(html).toContain('Close cycle')
    expect(html).toContain('Record payout')
  })

  it('does not render the Treasurer section at all for a Member', () => {
    const html = render(memberEntry)
    expect(html).not.toContain('Treasurer tasks')
    expect(html).not.toContain('Close cycle')
  })

  it('flips when ONLY the role changes', () => {
    expect(render({ ...memberEntry, role: 'Treasurer' })).toContain('Close cycle')
    expect(render({ ...treasurerEntry, role: 'Member' })).not.toContain('Close cycle')
  })

  it('also shows the tasks to a Chairperson', () => {
    expect(render({ ...memberEntry, role: 'Chairperson' })).toContain('Close cycle')
  })

  it('shows the role as text', () => {
    expect(render(treasurerEntry)).toContain('the Treasurer')
    expect(render(memberEntry)).toContain('a Member')
  })
})

describe('StokvelCard figures', () => {
  it('shows what is still owed when something is owed', () => {
    const html = render(treasurerEntry)
    expect(html).toContain('R 500,00')
    expect(html).not.toContain('All paid up')
  })

  it('says "All paid up for this cycle" when nothing is owed', () => {
    expect(render({ ...treasurerEntry, owedThisCycleCents: 0 })).toContain('All paid up for this cycle')
  })

  it('shows "No open cycle" and no paid or owed figures when there is no cycle', () => {
    const html = render({ ...treasurerEntry, currentCycle: null })
    expect(html).toContain('No open cycle')
    expect(html).not.toContain('Paid this cycle')
    expect(html).not.toContain('Still owed')
    expect(html).not.toContain('R ')
  })

  it('says "No payout scheduled" when there is no next payout date', () => {
    expect(render({ ...treasurerEntry, nextPayoutDate: null })).toContain('No payout scheduled')
  })

  it('says "No due date" when the cycle has no end date', () => {
    const cycle = { ...treasurerEntry.currentCycle!, endsOn: null }
    expect(render({ ...treasurerEntry, currentCycle: cycle })).toContain('No due date')
  })

  it('writes the cycle status as words, e.g. "Paid out"', () => {
    const cycle = { ...treasurerEntry.currentCycle!, status: 'PaidOut' as const }
    expect(render({ ...treasurerEntry, currentCycle: cycle })).toContain('Paid out')
  })

  it('never renders a stray "0" from a numeric condition', () => {
    // A bare 0 would appear as an element whose whole text is "0".
    const zeros = { ...memberEntry, paidThisCycleCents: 0, owedThisCycleCents: 0 }
    expect(render(zeros)).not.toMatch(/>0</)
    expect(render({ ...zeros, currentCycle: null })).not.toMatch(/>0</)
  })
})
