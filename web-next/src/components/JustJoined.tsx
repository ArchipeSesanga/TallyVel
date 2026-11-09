'use client'

import { useEffect } from 'react'
import { toast } from 'sonner'

/**
 * After joining, the join action sends the person to /stokvels/{id}?joined=1. This shows a short toast once and
 * takes "?joined=1" out of the address, so a reload doesn't say it again.
 */
export function JustJoined({ name }: { name: string }) {
  useEffect(() => {
    toast.success(`You joined ${name}.`)
    const url = new URL(window.location.href)
    url.searchParams.delete('joined')
    window.history.replaceState(window.history.state, '', url.pathname + url.search)
  }, [name])
  return null
}
