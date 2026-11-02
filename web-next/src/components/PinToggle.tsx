'use client'

import { Pin, PinOff } from 'lucide-react'
import { useEffect } from 'react'
import { usePins } from '@/components/PinsProvider'

/** Pin / Unpin for one stokvel. Pins keep only the id and the name, so nothing here can go stale except the name. */
export function PinToggle({ stokvelId, name }: { stokvelId: string; name: string }) {
  const ready = usePins((s) => s.ready)
  const pinned = usePins((s) => s.pins.find((p) => p.stokvelId === stokvelId))
  const pin = usePins((s) => s.pin)
  const unpin = usePins((s) => s.unpin)

  // A pinned stokvel that was renamed: keep the sidebar's name in step with the real one.
  useEffect(() => {
    if (ready && pinned !== undefined && pinned.name !== name) pin({ stokvelId, name })
  }, [ready, pinned, name, stokvelId, pin])

  const isPinned = pinned !== undefined
  return (
    <button
      type="button"
      // Until the saved pins are read we don't know the answer, so the button waits (disabled) instead of guessing.
      disabled={!ready}
      aria-pressed={isPinned}
      onClick={() => (isPinned ? unpin(stokvelId) : pin({ stokvelId, name }))}
      className="inline-flex cursor-pointer items-center gap-2 rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60"
    >
      {isPinned ? <PinOff className="size-4" aria-hidden="true" /> : <Pin className="size-4" aria-hidden="true" />}
      {isPinned ? 'Unpin' : 'Pin'}
      <span className="sr-only"> {name} {isPinned ? 'from' : 'to'} the sidebar</span>
    </button>
  )
}
