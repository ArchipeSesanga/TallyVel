'use client'

import dynamic from 'next/dynamic'
import { useState } from 'react'
import { Button } from '@/components/ui/button'

// Same idea as LazyCloseCycleDialog: the dialog's code is fetched when the person points at, tabs to or presses the button.
const load = () => import('@/components/LeaveStokvelDialog').then((m) => m.LeaveStokvelDialog)
const LeaveStokvelDialog = dynamic(load, {
  ssr: false,
  loading: () => <Button variant="outline" aria-busy="true" className="border-[#FDA29B] text-[#B42318]">Leave stokvel</Button>,
})

export function LazyLeaveStokvelDialog({ stokvelId, stokvelName, blocked = false }: { stokvelId: string; stokvelName: string; blocked?: boolean }) {
  const [wanted, setWanted] = useState(false)

  // The only Chairperson can't leave (the API refuses). The button says so by being disabled, and points at the
  // sentence that explains why (id="leave-reason" on the page). No dialog is needed, so none is ever loaded.
  if (blocked) {
    return (
      <Button type="button" variant="outline" disabled aria-describedby="leave-reason"
        className="cursor-not-allowed border-border bg-muted text-[#6B7480] opacity-100 disabled:opacity-100">
        Leave stokvel
      </Button>
    )
  }
  if (wanted) return <LeaveStokvelDialog stokvelId={stokvelId} stokvelName={stokvelName} startOpen />
  return (
    <Button
      variant="outline"
      aria-describedby="leave-reason"
      className="border-[#FDA29B] text-[#B42318]"
      onPointerEnter={() => void load()}
      onFocus={() => void load()}
      onClick={() => setWanted(true)}
    >
      Leave stokvel
    </Button>
  )
}
