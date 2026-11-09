'use client'

import { useActionState, useId, useState } from 'react'
import { toast } from 'sonner'
import { removeProof, uploadProof } from '@/app/proofActions'
import { PROOF_IDLE, type ProofState } from '@/lib/proofState'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { PROOF_ACCEPT, checkProofFileInBrowser } from '@/lib/proofFile'
import { localReference } from '@/lib/errorMessages'

type Props = {
  stokvelId: string
  contributionId: string
  hasProof: boolean
  /** The API said this person may download it (canDownloadProof). The API's 404 is what really protects the file. */
  canDownload: boolean
  /** The person may attach or replace one: it is theirs, or they are this stokvel's Treasurer, AND the cycle is open. */
  canChange: boolean
}

/** A toast that stays until it is closed, with the reference the person can quote. Used for the unexpected only. */
function showUnexpected(what: string, failure: { message: string; reference: string }) {
  toast.error(`${what}: ${failure.message}`, {
    description: `Reference: ${failure.reference}`,
    duration: Infinity, // does not disappear by itself
    closeButton: true,
  })
}

export function ProofControls({ stokvelId, contributionId, hasProof, canDownload, canChange }: Props) {
  const fileId = useId()
  const errorId = useId()
  // The browser's own check, shown under the input before anything is sent.
  const [clientError, setClientError] = useState<string | null>(null)
  const [hasFile, setHasFile] = useState(false)

  // The side effects (toasts) happen here, right after the server answers, not in an effect: a successful
  // upload calls refresh(), and the page may re-render before an effect would run.
  const [state, uploadAction, uploading] = useActionState(async (previous: ProofState, formData: FormData) => {
    try {
      const next = await uploadProof(previous, formData)
      if (next.status === 'done') toast.success('Proof uploaded.')
      if (next.unexpected !== null) showUnexpected("Couldn't upload the proof", next.unexpected)
      return next
    } catch (error) {
      // The request itself failed (offline, or Next refused a body over its limit). Not a wrong file: a toast that stays.
      console.error('uploadProof failed', error)
      const failure = { message: "The request didn't go through. Check your connection and try again.", reference: localReference() }
      showUnexpected("Couldn't upload the proof", failure)
      return { ...PROOF_IDLE, unexpected: failure }
    }
  }, PROOF_IDLE)

  const [removeState, removeAction, removing] = useActionState(async (previous: ProofState, formData: FormData) => {
    try {
      const next = await removeProof(previous, formData)
      if (next.status === 'removed') toast.success('Proof removed.')
      if (next.unexpected !== null) showUnexpected("Couldn't remove the proof", next.unexpected)
      return next
    } catch (error) {
      console.error('removeProof failed', error)
      const failure = { message: "The request didn't go through. Check your connection and try again.", reference: localReference() }
      showUnexpected("Couldn't remove the proof", failure)
      return { ...PROOF_IDLE, unexpected: failure }
    }
  }, PROOF_IDLE)

  const fileMessage = clientError ?? state.fileError
  const formMessage = state.message ?? removeState.message

  return (
    <div className="mt-3 space-y-2 border-t border-border pt-3 text-sm">
      {hasProof && (
        <p>
          Proof attached.{' '}
          {canDownload && (
            // A plain link (not next/link): it is a download, and must not be prefetched. The server checks who you are.
            <a href={`/bff/stokvels/${stokvelId}/contributions/${contributionId}/proof`} className="underline">Download proof</a>
          )}
        </p>
      )}
      {!hasProof && !canChange && <p className="text-muted-foreground">No proof attached.</p>}

      {canChange && (
        <form action={uploadAction} className="space-y-2" noValidate>
          <input type="hidden" name="stokvelId" value={stokvelId} />
          <input type="hidden" name="contributionId" value={contributionId} />
          <label htmlFor={fileId} className="block font-medium">
            {hasProof ? 'Replace the proof' : 'Add proof of payment'} <span className="font-normal text-muted-foreground">(JPEG, PNG or PDF, up to 5 MB)</span>
          </label>
          <Input
            id={fileId}
            name="file"
            type="file"
            accept={PROOF_ACCEPT}
            aria-invalid={fileMessage !== null}
            aria-describedby={fileMessage !== null ? errorId : undefined}
            onChange={async (event) => {
              const file = event.currentTarget.files?.[0]
              setHasFile(file !== undefined)
              setClientError(file === undefined ? null : await checkProofFileInBrowser(file))
            }}
            className="h-auto py-1"
          />
          {/* The wrong-file message lives here, under the input, whether the browser or the API found the problem. */}
          {fileMessage !== null && (
            <p id={errorId} role="alert" className="font-medium text-destructive">{fileMessage}</p>
          )}
          <Button type="submit" size="sm" disabled={uploading || !hasFile || clientError !== null}>
            {uploading ? 'Uploading…' : hasProof ? 'Replace proof' : 'Upload proof'}
          </Button>
        </form>
      )}

      {canChange && hasProof && (
        <form action={removeAction}>
          <input type="hidden" name="stokvelId" value={stokvelId} />
          <input type="hidden" name="contributionId" value={contributionId} />
          <Button type="submit" variant="ghost" size="sm" disabled={removing || uploading}>
            {removing ? 'Removing…' : 'Remove proof'}
          </Button>
        </form>
      )}

      {formMessage !== null && (
        <p role="alert" className="font-medium text-destructive">{formMessage}</p>
      )}
    </div>
  )
}
