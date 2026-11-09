'use client'

import { useActionState, useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { closeCycle, type CloseState } from '@/app/closeActions'
import {
  AlertDialog, AlertDialogCancel, AlertDialogContent, AlertDialogDescription,
  AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { BffRequestError, fetchBffJson } from '@/lib/bffClient'
import { formatDate } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { closeButtonLabel, closedToastText } from '@/lib/closeRules'
import { localReference } from '@/lib/errorMessages'
import type { ClosePreview } from '@/api/types'

type Preview =
  | { status: 'loading' }
  | { status: 'ready'; data: ClosePreview; changed: boolean }
  | { status: 'error'; message: string; reference: string | null }

type Props = {
  stokvelId: string; cycleId: string; cycleLabel: string; firstKey: string
  /** Open straight away. Used by LazyCloseCycleDialog, which only loads this file once the person has asked for it. */
  startOpen?: boolean
}

/**
 * "Close <cycle>…": a confirmation that says what will happen, from the API's own preview.
 * The browser never works out who owes what; it only shows the preview and sends back its `version`.
 */
export function CloseCycleDialog({ stokvelId, cycleId, cycleLabel, firstKey, startOpen = false }: Props) {
  const [open, setOpen] = useState(startOpen)
  const [preview, setPreview] = useState<Preview>({ status: 'loading' })
  // Whether to show the last answer: not when the dialog has just been opened again.
  const [showResult, setShowResult] = useState(false)
  const cancelRef = useRef<HTMLButtonElement>(null)
  const loadingRef = useRef<AbortController | null>(null)

  /** Asks the API for the preview and returns what to show. It sets no state itself, so an effect may use it too. */
  async function requestPreview(changed: boolean, signal: AbortSignal): Promise<Preview> {
    try {
      const data = await fetchBffJson<ClosePreview>(`/bff/stokvels/${stokvelId}/cycles/${cycleId}/close-preview`, signal)
      return { status: 'ready', data, changed }
    } catch (error) {
      const e = error instanceof BffRequestError ? error : null
      return { status: 'error', message: e?.message ?? 'Something went wrong. Please try again.', reference: e?.reference ?? null }
    }
  }

  async function loadPreview(changed: boolean) {
    loadingRef.current?.abort() // only the latest request may answer
    const controller = new AbortController()
    loadingRef.current = controller
    setPreview({ status: 'loading' })
    const next = await requestPreview(changed, controller.signal)
    if (!controller.signal.aborted) setPreview(next)
  }

  const initial: CloseState = { kind: 'idle', message: null, reference: null, idempotencyKey: firstKey, keyVersion: null, closed: null }
  const [state, formAction, pending] = useActionState(async (previous: CloseState, formData: FormData) => {
    let next: CloseState
    try {
      next = await closeCycle(previous, formData)
    } catch (error) {
      console.error('closeCycle failed', error)
      next = { ...previous, kind: 'failed', message: "The request didn't go through. Press the button again: trying again is safe, the arrears are recorded once.", reference: localReference(), closed: null }
    }
    setShowResult(true)
    if (next.kind === 'closed' && next.closed !== null) {
      setOpen(false)
      toast.success(closedToastText(cycleLabel, next.closed.totalArrearsCents, next.closed.memberCount))
      // The trigger may be gone after the refresh, so put focus somewhere sensible instead of on <body>.
      requestAnimationFrame(() => document.getElementById('stokvel-heading')?.focus())
    }
    if (next.kind === 'stale') void loadPreview(true) // show the NEW preview; the treasurer confirms again
    return next
  }, initial)

  function onOpenChange(next: boolean) {
    if (!next && pending) return // the close is running: the dialog stays until the API has answered
    setOpen(next)
    if (next) {
      setShowResult(false)
      void loadPreview(false)
    } else {
      loadingRef.current?.abort()
    }
  }

  // Opened on arrival (see startOpen): fetch the preview that onOpenChange(true) would have fetched. The preview
  // state already starts as 'loading', so nothing needs setting until the answer comes.
  useEffect(() => {
    if (!startOpen) return
    const controller = new AbortController()
    loadingRef.current = controller
    void requestPreview(false, controller.signal).then((next) => {
      if (!controller.signal.aborted) setPreview(next)
    })
    return () => controller.abort()
    // Once, when the dialog first appears: requestPreview only reads the ids above.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const ready = preview.status === 'ready' ? preview.data : null
  const alreadyClosed = showResult && state.kind === 'already-closed'

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogTrigger render={<Button variant="outline" />}>Close {cycleLabel}…</AlertDialogTrigger>
      {/* initialFocus: Cancel, the safe choice, never the button that does the damage. Esc closes (Base UI's default). */}
      <AlertDialogContent initialFocus={cancelRef} className="data-[size=default]:sm:max-w-md">
        <AlertDialogHeader>
          <AlertDialogTitle>Close {cycleLabel}</AlertDialogTitle>
          <AlertDialogDescription>
            {ready !== null
              ? `Counted up to ${formatDate(ready.asOfDate)}. This cannot be undone.`
              : 'Working out who would go into arrears…'}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <div aria-live="polite" className="space-y-3 text-sm">
          {preview.status === 'loading' && (
            <ul aria-busy="true" aria-label="Loading the close preview" className="space-y-2">
              {[1, 2, 3].map((n) => (
                <li key={n} className="flex justify-between gap-3">
                  <span className="h-5 w-32 animate-pulse rounded bg-muted" />
                  <span className="h-5 w-20 animate-pulse rounded bg-muted" />
                </li>
              ))}
            </ul>
          )}

          {preview.status === 'error' && (
            <p role="alert" className="font-medium text-destructive">
              {preview.message}{preview.reference !== null && <> Reference: <code className="font-mono">{preview.reference}</code></>}
            </p>
          )}

          {ready !== null && (
            <>
              {preview.status === 'ready' && preview.changed && (
                <p role="status" className="font-medium">Something changed since you looked. Check the new numbers, then confirm again.</p>
              )}
              {ready.arrears.length === 0 ? (
                <p>Nobody goes into arrears.</p>
              ) : (
                <>
                  <p>These members go into arrears:</p>
                  <ul className="divide-y divide-border rounded-lg border border-border">
                    {ready.arrears.map((a) => (
                      <li key={a.memberUserId} className="flex justify-between gap-3 px-3 py-2">
                        <span className="break-words">{a.fullName}</span>
                        <span className="whitespace-nowrap font-medium">{formatCents(a.shortfallCents)}</span>
                      </li>
                    ))}
                  </ul>
                  <p className="flex justify-between gap-3 font-semibold">
                    <span>Total</span>
                    <span className="whitespace-nowrap">{formatCents(ready.totalArrearsCents)}</span>
                  </p>
                </>
              )}
            </>
          )}

          {/* The API's answer when it says no, inside the dialog, with its reference. */}
          {showResult && state.message !== null && state.kind !== 'stale' && state.kind !== 'closed' && (
            <p role="alert" className="font-medium text-destructive">
              {state.message}{state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
            </p>
          )}
        </div>

        <form action={formAction}>
          <input type="hidden" name="stokvelId" value={stokvelId} />
          <input type="hidden" name="cycleId" value={cycleId} />
          <input type="hidden" name="version" value={ready?.version ?? ''} />
          {/* Written by the server; it changes only when a request is finished, so a retry sends the same one. */}
          <input type="hidden" name="idempotencyKey" value={state.idempotencyKey} />
          <AlertDialogFooter className="-mx-4 -mb-4">
            <AlertDialogCancel ref={cancelRef} disabled={pending}>{alreadyClosed ? 'Close' : 'Cancel'}</AlertDialogCancel>
            {!alreadyClosed && (
              <Button type="submit" variant="destructive" disabled={ready === null || pending}>
                {pending ? 'Closing…' : ready === null ? `Close ${cycleLabel}` : closeButtonLabel(cycleLabel, ready.totalArrearsCents)}
              </Button>
            )}
          </AlertDialogFooter>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  )
}
