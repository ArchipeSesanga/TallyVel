import type { ContributionItem, MeHomeEntry, StokvelDetail } from '@/api/types'

// Test data, TYPED with the generated API types (src/api/types.ts). If the API renames or drops a field, these
// stop compiling, so a test can't keep passing against a shape that no longer exists.

export const SIPHO = '0b9a7f68-5b5a-4a53-9a43-3c1d3e2a6f10'
export const LERATO = 'c67160fb-d6f0-40de-96e7-261823fef0f9'
export const NANDI = '5d1c6f3a-2f2e-4d0b-9a77-7a3a4f6f1b22'
export const THABO = 'a3b3d2f0-4c1e-4f5b-8f55-2b2f6a9a0c33'

export const WORK_ID = 'af302640-db0f-4d58-9c44-60dd025b39a7'
export const FAMILY_ID = '4ce16b73-25dd-4a88-b36c-e8b61a24bd65'

const joinedAt = '2026-01-05T08:00:00Z'

/** Work Stokvel: Sipho is its Treasurer. A cycle is open. */
export const workStokvel: StokvelDetail = {
  id: WORK_ID,
  name: 'Work Stokvel',
  contributionAmountCents: 30_000,
  cycle: 'Monthly',
  createdAt: joinedAt,
  members: [
    { userId: SIPHO, role: 'Treasurer', joinedAt, fullName: 'Sipho Zulu' },
    { userId: LERATO, role: 'Member', joinedAt, fullName: 'Lerato Mokoena' },
    { userId: NANDI, role: 'Chairperson', joinedAt, fullName: 'Nandi Mthembu' },
  ],
  memberCount: 3,
  currentCycle: { id: 'e2d7fa2f-7b25-48c1-93bb-5ae516603d75', label: '2026-11', status: 'Open', endsOn: '2026-11-30', collectedCents: 30_000, dueCents: 90_000 },
  rules: 'Everyone pays R300 a month on pay day.',
}

/** Family Stokvel: Sipho is only a Member. A cycle is open too. */
export const familyStokvel: StokvelDetail = {
  id: FAMILY_ID,
  name: 'Family Stokvel',
  contributionAmountCents: 50_000,
  cycle: 'Monthly',
  createdAt: joinedAt,
  members: [
    { userId: SIPHO, role: 'Member', joinedAt, fullName: 'Sipho Zulu' },
    { userId: LERATO, role: 'Treasurer', joinedAt, fullName: 'Lerato Mokoena' },
    { userId: THABO, role: 'Chairperson', joinedAt, fullName: 'Thabo Dlamini' },
  ],
  memberCount: 3,
  currentCycle: { id: '2cdde33d-9bd0-44e9-9104-748c48e60eeb', label: '2026-11', status: 'Open', endsOn: '2026-11-30', collectedCents: 50_000, dueCents: 150_000 },
  rules: null,
}

/** Sipho's own /me/home entry for each stokvel (his role comes from here). */
export const siphoInWork: MeHomeEntry = {
  stokvelId: WORK_ID,
  name: 'Work Stokvel',
  role: 'Treasurer',
  currentCycle: { id: 'e2d7fa2f-7b25-48c1-93bb-5ae516603d75', label: '2026-11', status: 'Open', startsOn: '2026-11-01', endsOn: '2026-11-30' },
  paidThisCycleCents: 0,
  owedThisCycleCents: 30_000,
  nextPayoutDate: '2026-11-30',
}

export const siphoInFamily: MeHomeEntry = {
  stokvelId: FAMILY_ID,
  name: 'Family Stokvel',
  role: 'Member',
  currentCycle: { id: '2cdde33d-9bd0-44e9-9104-748c48e60eeb', label: '2026-11', status: 'Open', startsOn: '2026-11-01', endsOn: '2026-11-30' },
  paidThisCycleCents: 0,
  owedThisCycleCents: 50_000,
  nextPayoutDate: '2026-11-30',
}

const contribution = (over: Partial<ContributionItem> & Pick<ContributionItem, 'id' | 'stokvelId' | 'memberUserId'>): ContributionItem => ({
  cycle: '2026-11',
  amountCents: 30_000,
  status: 'Paid',
  recordedAt: '2026-11-03T09:30:00Z',
  hasProof: false,
  canDownloadProof: false,
  ...over,
})

/** In Work, Lerato has paid and attached a proof. Sipho is the Treasurer, so the API says he may download it. */
export const workContributions: ContributionItem[] = [
  contribution({ id: 'c0000001-0000-4000-8000-000000000001', stokvelId: WORK_ID, memberUserId: LERATO, hasProof: true, canDownloadProof: true }),
  contribution({ id: 'c0000001-0000-4000-8000-000000000002', stokvelId: WORK_ID, memberUserId: NANDI }),
]

/** In Family, Thabo has a proof, but Sipho is only a member there, so the API says he may NOT download it. */
export const familyContributions: ContributionItem[] = [
  contribution({ id: 'c0000002-0000-4000-8000-000000000001', stokvelId: FAMILY_ID, memberUserId: THABO, hasProof: true, canDownloadProof: false, amountCents: 50_000 }),
  contribution({ id: 'c0000002-0000-4000-8000-000000000002', stokvelId: FAMILY_ID, memberUserId: LERATO, amountCents: 50_000 }),
  contribution({ id: 'c0000002-0000-4000-8000-000000000003', stokvelId: FAMILY_ID, memberUserId: SIPHO, amountCents: 50_000 }), // his own
]
