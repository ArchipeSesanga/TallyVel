import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import type { ContributionItem } from '../api/types'
import { contributions } from '../data/sample'
import { ContributionCard } from './ContributionCard'

function render(contribution: ContributionItem): string {
  return renderToStaticMarkup(<ContributionCard contribution={contribution} />)
}

describe('ContributionCard', () => {
  it('shows the midnight contribution on 1 November, not 31 October', () => {
    const midnight = contributions.find((c) => c.recordedAt === '2026-10-31T22:30:00Z')!
    expect(render(midnight)).toContain('01 Nov 2026, 00:30')
  })

  it('shows 10 cents as R 0,10 with the no-break space', () => {
    const tenCents = contributions.find((c) => c.amountCents === 10)!
    expect(render(tenCents)).toContain('R 0,10')
  })

  it('shows every status as text and the cycle it belongs to', () => {
    for (const status of ['Pending', 'Paid', 'Confirmed'] as const) {
      const c = contributions.find((x) => x.status === status)!
      const html = render(c)
      expect(html).toContain(status)
      expect(html).toContain(c.cycle)
    }
  })

  it('does not show who made the contribution', () => {
    const c = contributions[0]
    expect(render(c)).not.toContain(c.memberUserId)
  })
})
