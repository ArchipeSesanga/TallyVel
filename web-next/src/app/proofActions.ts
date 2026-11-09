'use server'

import { refresh } from 'next/cache'
import { redirect } from 'next/navigation'
import { api, correlationIdOf } from '@/lib/api'
import { getSession } from '@/lib/session'
import { loginPath } from '@/lib/loginPath'
import { describeApiError, localReference, tryAgainIn } from '@/lib/errorMessages'
import { FILE_EMPTY, FILE_TOO_BIG, FILE_WRONG_TYPE } from '@/lib/proofFile'
import { isUuid } from '@/lib/ids'
import { PROOF_IDLE, type ProofState } from '@/lib/proofState'

type Ids = { stokvelId: string; contributionId: string }

function idsFrom(formData: FormData): Ids | null {
  const stokvelId = String(formData.get('stokvelId') ?? '')
  const contributionId = String(formData.get('contributionId') ?? '')
  return isUuid(stokvelId) && isUuid(contributionId) ? { stokvelId, contributionId } : null
}

/** Turns the API's answer into a ProofState. Shared by upload and remove. */
function stateFromFailure(status: number, error: unknown, retryAfter: string | null): ProofState {
  const detail = typeof (error as { detail?: unknown })?.detail === 'string' ? (error as { detail: string }).detail : null
  const base = { ...PROOF_IDLE }
  if (status === 413) return { ...base, fileError: detail ?? FILE_TOO_BIG }
  if (status === 415) return { ...base, fileError: detail ?? FILE_WRONG_TYPE }
  if (status === 400) return { ...base, fileError: detail ?? FILE_EMPTY }
  if (status === 409) return { ...base, message: detail ?? 'The cycle is closed, so this proof can no longer be changed.' } // proof-locked
  if (status === 403) return { ...base, message: detail ?? describeApiError(403, null) } // proof-forbidden
  if (status === 404) return { ...base, message: "We couldn't find that payment. It may have been removed." }
  if (status === 429) return { ...base, message: tryAgainIn(retryAfter) }
  return {
    ...base,
    unexpected: {
      message: describeApiError(status, null),
      reference: correlationIdOf(error) ?? localReference(),
    },
  }
}

/** Attach or replace a proof. The browser has already checked size and type; the API checks again by the file's bytes. */
export async function uploadProof(_previous: ProofState, formData: FormData): Promise<ProofState> {
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const ids = idsFrom(formData)
  if (ids === null) return { ...PROOF_IDLE, message: "We couldn't find that payment." }

  const file = formData.get('file')
  if (!(file instanceof File) || file.size === 0) return { ...PROOF_IDLE, fileError: 'Choose a file first.' }

  const result = await api
    .POST('/api/v1/stokvels/{stokvelId}/contributions/{contributionId}/proof', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: ids },
      // The generated type calls the file a string (OpenAPI "binary"). We build the multipart body ourselves:
      // one field named "file", as the API expects. The name sent is just a label; the API ignores it for the type.
      body: { file: file as unknown as string },
      bodySerializer: (body) => {
        const form = new FormData()
        form.append('file', body.file as unknown as Blob, file.name)
        return form
      },
    })
    .catch((error: unknown) => {
      console.error('uploadProof: the API could not be reached', error)
      return null
    })

  if (result === null) {
    return { ...PROOF_IDLE, unexpected: { message: "Can't reach RondiTrack. Check your connection.", reference: localReference() } }
  }

  const status = result.response.status
  if (status === 401) redirect(await loginPath({ expired: true }))
  if (status === 200 || status === 201) {
    refresh() // the card shows "Proof attached" and the download link without a reload
    return { ...PROOF_IDLE, status: 'done' }
  }
  return stateFromFailure(status, result.error, result.response.headers.get('Retry-After'))
}

/** Remove a proof. Only offered while the cycle is open; the API refuses (409 proof-locked) after that. */
export async function removeProof(_previous: ProofState, formData: FormData): Promise<ProofState> {
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const ids = idsFrom(formData)
  if (ids === null) return { ...PROOF_IDLE, message: "We couldn't find that payment." }

  const result = await api
    .DELETE('/api/v1/stokvels/{stokvelId}/contributions/{contributionId}/proof', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: ids },
    })
    .catch(() => null)

  if (result === null) {
    return { ...PROOF_IDLE, unexpected: { message: "Can't reach RondiTrack. Check your connection.", reference: localReference() } }
  }
  const status = result.response.status
  if (status === 401) redirect(await loginPath({ expired: true }))
  if (status === 204 || status === 200) {
    refresh()
    return { ...PROOF_IDLE, status: 'removed' }
  }
  return stateFromFailure(status, result.error, result.response.headers.get('Retry-After'))
}
