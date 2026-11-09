'use client'

import dynamic from 'next/dynamic'
import { useState } from 'react'
import { Button } from '@/components/ui/button'

// The dialog is a lot of code (Base UI's dialog, the preview, the result handling) that nobody needs until they press
// the button. So the page ships only this button, and the dialog's code is fetched when the person points at it, tabs to
// it or presses it. Measured: the stokvel page's JavaScript is about 40 KB (gzipped) smaller before anyone clicks.
const load = () => import('@/components/CloseCycleDialog').then((m) => m.CloseCycleDialog)
const CloseCycleDialog = dynamic(load, {
  ssr: false,
  // While the code arrives the person still sees the same button, so nothing moves.
  loading: () => <Button variant="outline" aria-busy="true">Close …</Button>,
})

type Props = { stokvelId: string; cycleId: string; cycleLabel: string; firstKey: string }

export function LazyCloseCycleDialog(props: Props) {
  const [wanted, setWanted] = useState(false)
  if (wanted) return <CloseCycleDialog {...props} startOpen />
  return (
    <Button
      variant="outline"
      onPointerEnter={() => void load()}
      onFocus={() => void load()}
      onClick={() => setWanted(true)}
    >
      Close {props.cycleLabel}…
    </Button>
  )
}
