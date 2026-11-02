'use client'

import { createContext, use, useEffect, useState } from 'react'
import { useStore } from 'zustand'
import { clearAllSavedPins, createPinsStore, pinsStorageKey, type PinsState, type PinsStore } from '@/lib/pinsStore'

const PinsContext = createContext<PinsStore | null>(null)

/**
 * Gives the signed-in part of the site ONE pins store per provider (so one per browser tab and person).
 * It is created inside useState, never at the top of a module, and it only lives in the browser.
 */
export function PinsProvider({ userId, children }: { userId: string; children: React.ReactNode }) {
  const [store] = useState(() => createPinsStore(userId))

  useEffect(() => {
    // Read the pins saved in this browser AFTER the first render. The server's HTML and the first client render
    // both have no pins, so they match (no hydration warning); the saved pins then appear a moment later.
    let cancelled = false
    void Promise.resolve(store.persist.rehydrate()).then(() => {
      if (!cancelled) store.setState({ ready: true })
    })
    return () => {
      cancelled = true
    }
  }, [store])

  return <PinsContext value={store}>{children}</PinsContext>
}

/** Reads from the pins store of the nearest PinsProvider. */
export function usePins<T>(selector: (state: PinsState) => T): T {
  const store = use(PinsContext)
  if (store === null) throw new Error('usePins must be used inside <PinsProvider>')
  return useStore(store, selector)
}

/**
 * Returns a function that wipes this person's pins from the store AND from localStorage.
 * The Sign out buttons call it right before they submit the sign-out Server Action: that action runs on the
 * server and cannot touch the browser's storage, so the browser has to do this part itself.
 */
export function useClearPins(userId: string): () => void {
  const store = use(PinsContext)
  return () => {
    store?.getState().clear()
    store?.persist.clearStorage()
    try {
      window.localStorage.removeItem(pinsStorageKey(userId))
    } catch {
      /* storage is not available: nothing was saved */
    }
  }
}

/**
 * On the sign-in page: removes every saved pin in this browser. An expired or refused session cannot run the
 * Sign out button, but it always ends up on the sign-in page, so clearing here covers it.
 */
export function ClearPinsOnSignIn() {
  useEffect(() => {
    clearAllSavedPins()
  }, [])
  return null
}
