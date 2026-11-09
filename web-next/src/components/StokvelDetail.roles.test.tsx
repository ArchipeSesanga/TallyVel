// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ContributionItem, MeHomeEntry, StokvelDetail as StokvelDetailData } from '@/api/types'
import { PinsProvider } from '@/components/PinsProvider'
import { RecentContributionsList } from '@/components/RecentContributionsList'
import { StokvelDetail } from '@/components/StokvelDetail'
import { stokvelViewOf } from '@/lib/stokvelView'
import { familyContributions, familyStokvel, siphoInFamily, siphoInWork, SIPHO, workContributions, workStokvel } from '@/test/fixtures'

// The SAME person, Sipho, in two stokvels. In Work he is the Treasurer; in Family he is only a Member. The page must
// offer the treasurer's actions in the first and not in the second, and give no hint in the second that they exist.
// (This is what the page SHOWS. That the API refuses a Member who tries anyway is the end-to-end test's job.)

function renderStokvel(stokvel: StokvelDetailData, entry: MeHomeEntry, contributions: ContributionItem[]) {
  const view = stokvelViewOf(stokvel, entry)
  render(
    <QueryClientProvider client={new QueryClient()}>
      <PinsProvider userId={SIPHO}>
        <StokvelDetail
          view={view}
          youId={SIPHO}
          recordAction={async (previous) => previous} // never used: these tests only look at the page
          recordKey="11111111-1111-4111-8111-111111111111"
          closeKey="22222222-2222-4222-8222-222222222222"
          recent={
            <RecentContributionsList
              items={contributions}
              stokvelId={view.stokvelId}
              youId={SIPHO}
              canRun={view.canRun}
              openCycleLabel={view.cycle?.label ?? null}
              names={Object.fromEntries(view.members.map((m) => [m.userId, m.fullName ?? 'A member']))}
            />
          }
        />
      </PinsProvider>
    </QueryClientProvider>,
  )
}

describe('Sipho in Work Stokvel, where he is the Treasurer', () => {
  it('can record a payment for another member', () => {
    renderStokvel(workStokvel, siphoInWork, workContributions)

    expect(screen.getByRole('heading', { name: 'Work Stokvel' })).toBeInTheDocument()
    expect(screen.getByText("You're the Treasurer")).toBeInTheDocument()
    const payee = screen.getByRole('combobox', { name: 'Payment for' })
    expect(within(payee).getByRole('option', { name: 'Lerato Mokoena' })).toBeInTheDocument()
    expect(within(payee).getByRole('option', { name: 'Nandi Mthembu' })).toBeInTheDocument()
  })

  it('can close the cycle', () => {
    renderStokvel(workStokvel, siphoInWork, workContributions)

    expect(screen.getByRole('heading', { name: 'Close the cycle' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Close 2026-11…' })).toBeInTheDocument()
  })

  it('can invite people', () => {
    renderStokvel(workStokvel, siphoInWork, workContributions)

    expect(screen.getByRole('button', { name: 'Invite' })).toBeInTheDocument()
  })

  it('sees proof of payment: can download the one that was attached, and attach one where there is none', () => {
    renderStokvel(workStokvel, siphoInWork, workContributions)

    expect(screen.getByRole('link', { name: 'Download proof' })).toHaveAttribute('href', expect.stringMatching(/\/proof$/))
    expect(screen.getByLabelText(/Replace the proof/)).toBeInTheDocument() // Lerato's, which has one
    expect(screen.getByLabelText(/Add proof of payment/)).toBeInTheDocument() // Nandi's, which has none
  })

  it('cannot change roles: only a Chairperson can, and he is not one', () => {
    renderStokvel(workStokvel, siphoInWork, workContributions)

    expect(screen.queryByRole('combobox', { name: /^Role of/ })).not.toBeInTheDocument()
  })
})

describe('Sipho in Family Stokvel, where he is only a Member', () => {
  it('can only record a payment for himself, so there is no "Payment for" box at all', () => {
    renderStokvel(familyStokvel, siphoInFamily, familyContributions)

    expect(screen.getByRole('heading', { name: 'Family Stokvel' })).toBeInTheDocument()
    expect(screen.getByText("You're a Member")).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Record a contribution' })).toBeInTheDocument() // his own payment
    expect(screen.queryByRole('combobox', { name: 'Payment for' })).not.toBeInTheDocument()
  })

  it('has no way to close the cycle, and nothing on the page mentions closing one', () => {
    renderStokvel(familyStokvel, siphoInFamily, familyContributions)

    expect(screen.queryByRole('button', { name: /^Close / })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /Close the cycle/ })).not.toBeInTheDocument()
    expect(screen.queryByText(/close the cycle/i)).not.toBeInTheDocument()
  })

  it('has no way to invite people, and no invite wording anywhere', () => {
    renderStokvel(familyStokvel, siphoInFamily, familyContributions)

    expect(screen.queryByRole('button', { name: /invite/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/invite/i)).not.toBeInTheDocument()
  })

  it('cannot change roles', () => {
    renderStokvel(familyStokvel, siphoInFamily, familyContributions)

    expect(screen.queryByRole('combobox', { name: /^Role of/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument()
  })

  it('cannot download a proof the API says he may not, and cannot attach one to someone else\'s payment', () => {
    renderStokvel(familyStokvel, siphoInFamily, familyContributions)

    expect(screen.queryByRole('link', { name: /download proof/i })).not.toBeInTheDocument()
    // The only attach box is on his OWN payment.
    const attach = screen.getAllByLabelText(/Add proof of payment/)
    expect(attach).toHaveLength(1)
    expect(attach[0].closest('li')).toHaveTextContent('You')
  })

  it('can still leave', () => {
    renderStokvel(familyStokvel, siphoInFamily, familyContributions)

    expect(screen.getByRole('button', { name: 'Leave stokvel' })).toBeEnabled()
  })
})
