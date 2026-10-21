import type { ContributionItem, MeHomeEntry } from '../api/types'

// Captured from the real API as Lerato (lerato@tallyvel.test) on 2026-10-21, from the seeded
// demo data. Every name is made up. The types come from the generated schema, so if the API's
// shape changes this file stops compiling instead of silently going stale.
// A later assignment replaces these constants with live calls; the cards only take props.

// GET /api/v1/me/home -> .stokvels
export const home: MeHomeEntry[] = [
  {
    stokvelId: 'b12b7944-7790-4aa6-a450-535434d34b05',
    name: 'Family Stokvel',
    role: 'Treasurer',
    currentCycle: {
      id: '95e271c1-f7a6-4ac5-afea-ed87dc2bd64a',
      label: '2026-10',
      status: 'Open',
      startsOn: '2026-10-01',
      endsOn: '2026-10-31',
    },
    paidThisCycleCents: 0,
    owedThisCycleCents: 50000,
    nextPayoutDate: '2026-10-21',
  },
  {
    stokvelId: '210ef486-282d-447e-8ad1-a70a5e5c26a5',
    name: 'Work Stokvel',
    role: 'Member',
    currentCycle: {
      id: '06480ad5-0fc3-4296-a97d-ef4f0f13ec8e',
      label: '2026-10',
      status: 'Open',
      startsOn: '2026-10-01',
      endsOn: '2026-10-31',
    },
    paidThisCycleCents: 30000,
    owedThisCycleCents: 0,
    nextPayoutDate: '2026-10-21',
  },
]

// The stokvel whose contributions are listed under the cards.
export const chosenStokvelId = 'b12b7944-7790-4aa6-a450-535434d34b05'
export const chosenStokvelName = 'Family Stokvel'

// GET /api/v1/stokvels/{chosenStokvelId}/contributions -> .items
export const contributions: ContributionItem[] = [
  {
    id: 'fbe8b101-2987-4772-acfe-ffa58609f425',
    stokvelId: 'b12b7944-7790-4aa6-a450-535434d34b05',
    memberUserId: '344cd664-770d-43e7-9363-1c6854d26dd8',
    cycle: '2026-10',
    amountCents: 50000,
    status: 'Confirmed',
    recordedAt: '2026-10-18T02:20:56.824059Z',
  },
  {
    id: 'f982b99d-868e-42cf-b93f-3fe8f550dd92',
    stokvelId: 'b12b7944-7790-4aa6-a450-535434d34b05',
    memberUserId: 'af9f7170-bc83-4eb8-af79-eed96e225cc9',
    cycle: '2026-10',
    amountCents: 50000,
    status: 'Paid',
    recordedAt: '2026-10-19T02:20:56.824059Z',
  },
  {
    id: 'ed0308db-1214-41da-897e-8052da7cc1eb',
    stokvelId: 'b12b7944-7790-4aa6-a450-535434d34b05',
    memberUserId: '844d4033-6ccf-477b-b956-355ee30f7bc4',
    cycle: '2026-10',
    amountCents: 50000,
    status: 'Pending',
    recordedAt: '2026-10-20T02:20:56.824059Z',
  },
  {
    id: '07c44042-6537-4d9c-b85a-c67bbc29723b',
    stokvelId: 'b12b7944-7790-4aa6-a450-535434d34b05',
    memberUserId: '06b2d247-29b7-42d0-a19c-83d327e07c2d',
    cycle: '2026-10',
    amountCents: 10,
    status: 'Paid',
    recordedAt: '2026-10-20T14:20:56.824059Z',
  },
  {
    id: '517c6be0-44f0-4c1a-b655-dda1442eb0b1',
    stokvelId: 'b12b7944-7790-4aa6-a450-535434d34b05',
    memberUserId: '06b2d247-29b7-42d0-a19c-83d327e07c2d',
    cycle: '2026-11',
    amountCents: 50000,
    status: 'Paid',
    recordedAt: '2026-10-31T22:30:00Z',
  },
]
