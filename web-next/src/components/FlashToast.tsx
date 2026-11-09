'use client'

import { useEffect } from 'react'
import { toast } from 'sonner'

const FLASH_PARAMS = ['joined', 'created', 'left']

/**
 * After joining, creating or leaving, the Server Action redirects with a marker (?joined=1, ?created=1, ?left=1).
 * This shows one short toast and takes the marker out of the address, so a reload doesn't say it again.
 * The page decides the message (it knows the stokvel's name); the marker is only a switch.
 */
export function FlashToast({ message }: { message: string }) {
  useEffect(() => {
    toast.success(message, { id: 'flash' }) // a fixed id: React's dev double-run of effects shows ONE toast, not two
    const url = new URL(window.location.href)
    for (const name of FLASH_PARAMS) url.searchParams.delete(name)
    window.history.replaceState(window.history.state, '', url.pathname + url.search)
  }, [message])
  return null
}
