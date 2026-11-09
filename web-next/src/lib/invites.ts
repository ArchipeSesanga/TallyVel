import 'server-only'
import { redirect } from 'next/navigation'
import { api, correlationIdOf } from '@/lib/api'
import { getSession } from '@/lib/session'
import { loginPath } from '@/lib/loginPath'
import { describeApiError } from '@/lib/errorMessages'
import type { InvitePreview } from '@/api/types'
import type { PreviewKind } from '@/lib/joinFlow'

/** What the API said about a code. `kind` is what joinFlow.decideStep looks at; the rest is for the screen. */
export type PreviewOutcome =
  | { kind: 'valid'; preview: InvitePreview }
  | { kind: 'already-member'; stokvelName: string }
  | { kind: 'not-found' }
  | { kind: 'expired' }
  | { kind: 'rate-limited'; retryAfter: string | null }
  | { kind: 'failed'; message: string; reference: string | null }

export function kindOf(outcome: PreviewOutcome | null): PreviewKind | null {
  return outcome === null ? null : outcome.kind
}

/**
 * Asks the API what an invite code is. Every answer is a value, never an error page: a wrong, expired or
 * rate-limited code is something a person can be told about on the screen. Only "not signed in" leaves the page.
 */
export async function fetchInvitePreview(code: string): Promise<PreviewOutcome> {
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const result = await api
    .GET('/api/v1/invites/{code}', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: { code } },
    })
    .catch(() => null)

  if (result === null) return { kind: 'failed', message: "Can't reach RondiTrack. Check your connection.", reference: null }

  const { data, error, response } = result
  if (data !== undefined) {
    // The API says whether the caller is in already. No point walking them through joining.
    return data.alreadyMember ? { kind: 'already-member', stokvelName: data.stokvelName } : { kind: 'valid', preview: data }
  }

  if (response.status === 401) redirect(await loginPath({ expired: true }))
  if (response.status === 404) return { kind: 'not-found' }
  if (response.status === 410) return { kind: 'expired' }
  if (response.status === 429) return { kind: 'rate-limited', retryAfter: response.headers.get('Retry-After') }
  return { kind: 'failed', message: describeApiError(response.status, null), reference: correlationIdOf(error) }
}

/**
 * The preview does not carry the stokvel's id (a non-member has no use for it), so for "you're already a member"
 * we look the stokvel up among the person's OWN stokvels by name. If two of theirs share that name we can't tell
 * which one it is, so the caller falls back to the stokvel list.
 */
export async function ownStokvelIdNamed(name: string): Promise<string | null> {
  const session = await getSession()
  if (session === null) return null
  const home = await api
    .GET('/api/v1/me/home', { headers: { Authorization: `Bearer ${session.accessToken}` } })
    .catch(() => null)
  const matches = home?.data?.stokvels.filter((s) => s.name === name) ?? []
  return matches.length === 1 ? matches[0].stokvelId : null
}
