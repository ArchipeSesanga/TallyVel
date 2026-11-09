import { formatCents } from './money'

// The rules of closing a cycle that are plain decisions (no network, no screen), so they can be tested.

/**
 * Which Idempotency-Key does a close send? A key belongs to ONE request: the same cycle and the same preview `version`.
 * It is reused after an unknown outcome (no answer, a 5xx) so a retry cannot close twice, and it is replaced when the
 * version differs from the one it was last sent with, because that is a different request (the API would answer 422).
 */
export function chooseCloseKey(keyInForm: string, keyVersion: string | null, version: string, newKey: () => string): string {
  const keyWasUsedForAnotherVersion = keyVersion !== null && keyVersion !== version
  const keyIsUsable = /^[0-9a-f-]{36}$/i.test(keyInForm)
  return keyIsUsable && !keyWasUsedForAnotherVersion ? keyInForm : newKey()
}

/** The confirm button says exactly what it will do, with the amount the API's preview worked out. Never "OK". */
export function closeButtonLabel(cycleLabel: string, totalArrearsCents: number): string {
  return totalArrearsCents === 0
    ? `Close ${cycleLabel} with no arrears`
    : `Close ${cycleLabel} and record ${formatCents(totalArrearsCents)} in arrears`
}

/** The toast after a successful close: what was recorded. */
export function closedToastText(cycleLabel: string, totalArrearsCents: number, memberCount: number): string {
  if (memberCount === 0) return `Closed ${cycleLabel}. No arrears were recorded.`
  return `Closed ${cycleLabel}. Recorded ${formatCents(totalArrearsCents)} in arrears for ${memberCount} ${memberCount === 1 ? 'member' : 'members'}.`
}
