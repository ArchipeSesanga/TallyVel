// The join flow in three steps, as plain functions (no React, no server code) so every rule can be tested.
//
//   code    → type or paste the invite code
//   rules   → read what the stokvel is (name, rules, amount, schedule, member count)
//   confirm → press "Join <name>"
//
// The step lives in the address (/join?code=…&step=rules). That is what makes Back, a reload and the invite
// link itself land on the right screen. The ONE function that decides which step may be shown is decideStep.

import type { ContributionFrequency } from '@/api/types'

export const JOIN_STEPS = ['code', 'rules', 'confirm'] as const
export type JoinStep = (typeof JOIN_STEPS)[number]

/** What the API said about the code. 'valid' is the only answer that lets the person past step 1. */
export type PreviewKind = 'valid' | 'not-found' | 'expired' | 'already-member' | 'rate-limited' | 'failed'

/** An invite code is 22 URL-safe characters. We accept up to 64 so a slightly wrong paste still gets the API's answer (404). */
export const CODE_PATTERN = /^[A-Za-z0-9_-]{1,64}$/

/**
 * Which step to show. `requested` is the step in the address (null: none, or not one of ours).
 *
 * - No usable code: only step 1 makes sense.
 * - Step 1 is always allowed once you are there (that is where Back goes).
 * - Steps 2 and 3 need an invite the API accepted.
 * - An address with a code but no step (the invite link itself) means step 2.
 *
 * The page redirects whenever the answer differs from `requested`, so the address always says the step shown.
 */
export function decideStep(requested: JoinStep | null, hasCode: boolean, preview: PreviewKind | null): JoinStep {
  if (!hasCode) return 'code'
  if (requested === 'code') return 'code'
  if (preview !== 'valid') return 'code'
  return requested ?? 'rules'
}

/** The one address for a step. Step 1 without a code is /join?step=code, so "/join" itself redirects there and never loops. */
export function stepUrl(step: JoinStep, code: string | null): string {
  const params = new URLSearchParams()
  if (code !== null) params.set('code', code)
  params.set('step', step)
  return `/join?${params}`
}

/** The invite link a treasurer shares. `origin` is e.g. "https://tallyvel.example". */
export function inviteLink(origin: string, code: string): string {
  return `${origin}${stepUrl('rules', code)}`
}

const FREQUENCY_WORD: Record<ContributionFrequency, string> = {
  Weekly: 'Weekly',
  BiWeekly: 'Every two weeks',
  Monthly: 'Monthly',
}

/**
 * "Weekly (every 7 days)" or "Monthly". cycleLengthDays is null for monthly stokvels (a calendar month has no
 * fixed number of days), so it is only mentioned when there is a number. Never "null".
 */
export function scheduleText(frequency: ContributionFrequency, cycleLengthDays: number | null): string {
  const word = FREQUENCY_WORD[frequency]
  return cycleLengthDays === null ? word : `${word} (every ${cycleLengthDays} days)`
}

export const JOIN_MESSAGES = {
  'not-found': 'Check the code and try again.',
  expired: 'This invite has expired. Ask the treasurer for a new one.',
} as const

/**
 * What was typed or pasted into the code box, as a code. People paste the whole invite link
 * ("https://site/join?code=XYZ&step=rules"), so if the text is a link with a ?code=, that is the code.
 * Otherwise the text itself, trimmed. (Whether it is a real code is for CODE_PATTERN and the API.)
 */
export function extractCode(typed: string): string {
  const text = typed.trim()
  if (!/[/?=]/.test(text)) return text
  try {
    const fromLink = new URL(text, 'http://our.site').searchParams.get('code')
    if (fromLink !== null && fromLink.trim() !== '') return fromLink.trim()
  } catch {
    // not a link: fall through
  }
  return text
}
