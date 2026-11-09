'use client'

import { useActionState, useRef, useState } from 'react'
import { leaveStokvel, type LeaveState } from '@/app/stokvelActions'
import {
  AlertDialog, AlertDialogCancel, AlertDialogContent, AlertDialogDescription,
  AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'

/**
 * "Leave <stokvel>…": says what leaving does before it happens. Focus starts on Cancel; Esc closes (not while
 * the request runs); a "no" from the API (e.g. the only Chairperson) is shown inside the dialog.
 */
export function LeaveStokvelDialog({ stokvelId, stokvelName, blocked = false }: { stokvelId: string; stokvelName: string; blocked?: boolean }) {
  const [open, setOpen] = useState(false)
  const cancelRef = useRef<HTMLButtonElement>(null)
  const [state, formAction, pending] = useActionState(leaveStokvel, { message: null, reference: null } as LeaveState)

  // The only Chairperson can't leave (the API refuses). The button says so by being disabled, and points at the
  // sentence that explains why (id="leave-reason" on the page).
  if (blocked) {
    return (
      <Button type="button" variant="outline" disabled aria-describedby="leave-reason"
        className="cursor-not-allowed border-border bg-muted text-[#6B7480] opacity-100 disabled:opacity-100">
        Leave stokvel
      </Button>
    )
  }

  return (
    <AlertDialog open={open} onOpenChange={(next) => { if (!next && pending) return; setOpen(next) }}>
      <AlertDialogTrigger render={<Button variant="outline" aria-describedby="leave-reason" className="border-[#FDA29B] text-[#B42318]" />}>Leave stokvel</AlertDialogTrigger>
      <AlertDialogContent initialFocus={cancelRef} className="data-[size=default]:sm:max-w-md">
        <AlertDialogHeader>
          <AlertDialogTitle>Leave {stokvelName}</AlertDialogTitle>
          <AlertDialogDescription>
            You will stop seeing this stokvel and its contributions. Your past payments stay on its record.
            To join again you will need a new invite.
          </AlertDialogDescription>
        </AlertDialogHeader>
        {state.message !== null && (
          <p role="alert" className="text-sm font-medium text-destructive">
            {state.message}{state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
          </p>
        )}
        <form action={formAction}>
          <input type="hidden" name="stokvelId" value={stokvelId} />
          <AlertDialogFooter className="-mx-4 -mb-4">
            <AlertDialogCancel ref={cancelRef} disabled={pending}>Cancel</AlertDialogCancel>
            <Button type="submit" variant="destructive" disabled={pending}>{pending ? 'Leaving…' : `Leave ${stokvelName}`}</Button>
          </AlertDialogFooter>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  )
}
