'use client'

import { Pin, PinOff } from 'lucide-react'
import { useEffect } from 'react'
import { usePins } from '@/components/PinsProvider'
import { Button } from '@/components/ui/button'

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
    <Button
      type="button"
      variant="outline"
      // Until the saved pins are read we don't know the answer, so the button waits (disabled) instead of guessing.
      disabled={!ready}
      aria-pressed={isPinned}
      onClick={() => (isPinned ? unpin(stokvelId) : pin({ stokvelId, name }))}
    >
      {isPinned ? <PinOff aria-hidden="true" /> : <Pin aria-hidden="true" />}
      {isPinned ? 'Unpin' : 'Pin to sidebar'}
      <span className="sr-only"> {isPinned ? `${name} from the sidebar` : `(${name})`}</span>
    </Button>
  )
}
