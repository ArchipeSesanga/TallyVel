'use server'

import { revalidatePath } from 'next/cache'
import { redirect } from 'next/navigation'
import { z } from 'zod'
import { api, correlationIdOf } from '@/lib/api'
import { getSession } from '@/lib/session'
import { loginPath } from '@/lib/loginPath'
import { describeApiError, localReference, tryAgainIn } from '@/lib/errorMessages'
import { CODE_PATTERN, JOIN_MESSAGES } from '@/lib/joinFlow'
import { isUuid } from '@/lib/ids'
import { fetchInvitePreview } from '@/lib/invites'

export type JoinState = {
  /** What went wrong, or null. Always shown to the person. */
  message: string | null
  /** 'already-member' gets a link to the stokvel; 'start-again' gets a link back to step 1; 'retry' is "press the button again". */
  next: 'retry' | 'already-member' | 'start-again'
  /** The stokvel they are already in (from the API's 409). Only set with next = 'already-member'. */
  stokvelId: string | null
  reference: string | null
  /** The key the next press sends. The server wrote it; it only changes when a join is finished one way or the other. */
  idempotencyKey: string
}

const MAY_NOT_HAVE_WORKED =
  "We couldn't confirm that you joined. Press the button again: trying again is safe, you can't join twice."

/**
 * Step 3's button. The person's own token goes to the API, with the Idempotency-Key the server wrote into the form:
 * - a double click or a retry after "unknown" sends the SAME key, so the API answers with the first result;
 * - the API also lets only one membership exist per person per stokvel, so even a lost key can't join twice.
 */
export async function joinStokvel(previous: JoinState, formData: FormData): Promise<JoinState> {
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const code = String(formData.get('code') ?? '')
  const keyInForm = String(formData.get('idempotencyKey') ?? '')
  const idempotencyKey = z.uuid().safeParse(keyInForm).success ? keyInForm : crypto.randomUUID()
  const failed = (patch: Partial<JoinState>): JoinState => ({
    message: null, next: 'retry', stokvelId: null, reference: null, ...patch, idempotencyKey,
  })

  if (!CODE_PATTERN.test(code)) return failed({ message: JOIN_MESSAGES['not-found'], next: 'start-again' })

  const result = await api
    .POST('/api/v1/invites/{code}/join', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: { code }, header: { 'Idempotency-Key': idempotencyKey } },
    })
    .catch((error: unknown) => {
      console.error('join: the API could not be reached', error)
      return null
    })

  // No answer at all: it may or may not have worked. Same key on the next press.
  if (result === null) return failed({ message: MAY_NOT_HAVE_WORKED, reference: localReference() })

  const { data, error, response } = result
  if (data !== undefined) {
    // The shell's list of "my stokvels" changed, so it must be fetched again, then off to the stokvel.
    revalidatePath('/', 'layout')
    redirect(`/stokvels/${data.stokvelId}?joined=1`)
  }

  const status = response.status
  const reference = correlationIdOf(error)
  if (status === 401) redirect(await loginPath({ expired: true }))
  if (status === 404) return failed({ message: JOIN_MESSAGES['not-found'], next: 'start-again', reference })
  if (status === 410) return failed({ message: JOIN_MESSAGES.expired, next: 'start-again', reference })
  if (status === 409) {
    const stokvelId = (error as { stokvelId?: unknown } | undefined)?.stokvelId
    return failed({ message: "You're already a member", next: 'already-member', stokvelId: typeof stokvelId === 'string' && isUuid(stokvelId) ? stokvelId : null, reference })
  }
  if (status === 429) return failed({ message: tryAgainIn(response.headers.get('Retry-After')), reference })
  if (status >= 500) return failed({ message: MAY_NOT_HAVE_WORKED, reference })
  return failed({ message: describeApiError(status, null), reference })
}

export type CreateInviteState = {
  code: string | null
  expiresAt: string | null
  message: string | null
  reference: string | null
}

/** A treasurer's "Create invite link". The API decides who may (Treasurer or Chairperson of THIS stokvel); a 403/404 is shown as a message. */
export async function createInvite(_previous: CreateInviteState, formData: FormData): Promise<CreateInviteState> {
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const stokvelId = String(formData.get('stokvelId') ?? '')
  const none = { code: null, expiresAt: null, reference: null }
  if (!isUuid(stokvelId)) return { ...none, message: "We couldn't find that stokvel." }

  const result = await api
    .POST('/api/v1/stokvels/{stokvelId}/invites', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: { stokvelId } },
    })
    .catch(() => null)

  if (result === null) return { ...none, message: "Can't reach RondiTrack. Check your connection.", reference: localReference() }
  if (result.data !== undefined) return { code: result.data.code, expiresAt: result.data.expiresAt, message: null, reference: null }

  const status = result.response.status
  if (status === 401) redirect(await loginPath({ expired: true }))
  return { ...none, message: describeApiError(status, result.response.headers.get('Retry-After')), reference: correlationIdOf(result.error) }
}

export type CheckInviteResult = { ok: true } | { ok: false; kind: 'not-found' | 'expired' | 'already-member' | 'rate-limited' | 'failed'; detail: string | null }

/**
 * The header's "Join" box: is this a code we can take to step 2? (It does NOT join: joining stays in the
 * three steps, so the person always sees the rules and confirms.) Only the kind of answer goes back.
 */
export async function checkInviteCode(code: string): Promise<CheckInviteResult> {
  if (!CODE_PATTERN.test(code)) return { ok: false, kind: 'not-found', detail: null }
  const outcome = await fetchInvitePreview(code)
  switch (outcome.kind) {
    case 'valid':
      return { ok: true }
    case 'rate-limited':
      return { ok: false, kind: 'rate-limited', detail: outcome.retryAfter }
    case 'failed':
      return { ok: false, kind: 'failed', detail: outcome.reference }
    default:
      return { ok: false, kind: outcome.kind, detail: null }
  }
}
