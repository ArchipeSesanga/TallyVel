import type { MemberRole } from '../api/types'

/**
 * The API lets a Treasurer OR a Chairperson run a cycle (close it, record the payout).
 * This is the one place the frontend repeats that rule, so the cards never compare role
 * strings themselves. MemberRole comes from the API's schema, so a typo such as
 * 'Treasuer' will not compile.
 *
 * This only decides what to SHOW. It is not security: the API still checks the role.
 */
export function canRunCycle(role: MemberRole): boolean {
  return role === 'Treasurer' || role === 'Chairperson'
}

// A Record keyed by MemberRole must have every role, so adding a role to the API
// without adding its label here is a compile error.
const roleLabels: Record<MemberRole, string> = {
  Member: "You're a Member",
  Treasurer: "You're the Treasurer",
  Chairperson: "You're the Chairperson",
}

export function roleLabel(role: MemberRole): string {
  return roleLabels[role]
}
