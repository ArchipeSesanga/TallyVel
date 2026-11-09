'use server'

import { refresh } from 'next/cache'
import { redirect } from 'next/navigation'
import { api, correlationIdOf } from '@/lib/api'
import { getSession } from '@/lib/session'
import { loginPath } from '@/lib/loginPath'
import { describeApiError, localReference, tryAgainIn } from '@/lib/errorMessages'
import { isUuid } from '@/lib/ids'
import { chooseCloseKey } from '@/lib/closeRules'

export type CloseState = {
  /**
   * idle: nothing sent yet. closed: done. stale: the numbers changed, nothing was closed (the dialog fetches the
   * new preview). already-closed: someone else got there first. refused: the API said no (e.g. 403).
   * failed: no answer or a 5xx; the outcome is unknown, so pressing again is safe (same key).
   */
  kind: 'idle' | 'closed' | 'stale' | 'already-closed' | 'refused' | 'failed'
  message: string | null
  reference: string | null
  /** The key the next press sends. The server writes it. */
  idempotencyKey: string
  /** The preview version the current key was last sent with. A different version is a different request: a new key. */
  keyVersion: string | null
  /** What was recorded, from the API's own answer. */
  closed: { totalArrearsCents: number; memberCount: number } | null
}

const MAY_NOT_HAVE_WORKED = "We couldn't confirm whether the cycle was closed. Press the button again: trying again is safe, the arrears are recorded once."

/**
 * Closes the cycle the treasurer was shown. It sends the preview's `version` (so the API refuses if anything
 * changed since) and an Idempotency-Key (so a double click or a retry is the same request, not a second close).
 */
export async function closeCycle(previous: CloseState, formData: FormData): Promise<CloseState> {
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const stokvelId = String(formData.get('stokvelId') ?? '')
  const cycleId = String(formData.get('cycleId') ?? '')
  const version = String(formData.get('version') ?? '')
  const keyInForm = String(formData.get('idempotencyKey') ?? '')

  // A key belongs to ONE request. If the version differs from the one this key was last sent with, it is a
  // different request (the API would answer 422 to a reused key with another body), so it gets a new key.
  const key = chooseCloseKey(keyInForm, previous.keyVersion, version, () => crypto.randomUUID())

  const answer = (kind: CloseState['kind'], patch: Partial<CloseState>, rotateKey: boolean): CloseState => ({
    kind, message: null, reference: null, closed: null, ...patch,
    idempotencyKey: rotateKey ? crypto.randomUUID() : key,
    keyVersion: rotateKey ? null : version,
  })

  if (!isUuid(stokvelId) || !isUuid(cycleId) || version === '' || version.length > 200) {
    return answer('refused', { message: "We couldn't find that cycle." }, true)
  }

  const result = await api
    .POST('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/close', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: { stokvelId, cycleId }, header: { 'Idempotency-Key': key } },
      body: { version },
    })
    .catch((error: unknown) => {
      console.error('closeCycle: the API could not be reached', error)
      return null
    })

  // No answer: the close may have happened. Same key, so a retry cannot record the arrears twice.
  if (result === null) return answer('failed', { message: MAY_NOT_HAVE_WORKED, reference: localReference() }, false)

  const { data, error, response } = result
  const status = response.status
  const reference = correlationIdOf(error)
  const code = (error as { code?: unknown } | undefined)?.code
  const detail = typeof (error as { detail?: unknown } | undefined)?.detail === 'string' ? (error as { detail: string }).detail : null

  if (status === 401) redirect(await loginPath({ expired: true }))

  if (data !== undefined) {
    refresh() // the page shows the new state of the cycle
    return answer('closed', { closed: { totalArrearsCents: data.totalArrearsCents, memberCount: data.arrears.length } }, true)
  }

  // Something changed since the treasurer looked. Nothing was closed; the dialog fetches the new preview.
  if (status === 409 && code === 'close-preview-stale') {
    return answer('stale', { message: detail ?? 'Something changed since you looked. Nothing was closed.', reference }, true)
  }
  if (status === 409) {
    refresh() // closed already (nightly job or another treasurer): show the page as it is now
    return answer('already-closed', { message: detail ?? 'This cycle was already closed.', reference }, true)
  }
  // A refusal (403 not a treasurer, 404): the API's own words, and nobody is signed out.
  if (status === 403 || status === 404) {
    return answer('refused', { message: detail ?? describeApiError(status === 403 ? 403 : 404, null), reference }, true)
  }
  if (status === 429) return answer('failed', { message: tryAgainIn(response.headers.get('Retry-After')), reference }, false)
  if (status >= 500) return answer('failed', { message: MAY_NOT_HAVE_WORKED, reference }, false)
  return answer('refused', { message: detail ?? describeApiError(status, null), reference }, true)
}
