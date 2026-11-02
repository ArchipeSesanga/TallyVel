// The pinned stokvels: the ONLY thing kept in Zustand. A pin is a stokvel's id and name, nothing else.
// No balance, role or member list, because those come from the API and would go out of date here.
//
// Zustand's rules for Next.js, which this file follows:
//   - a store FACTORY (createStore from "zustand/vanilla"), never a store at the top of the module:
//     a module-level store on the server would be shared by every visitor;
//   - one store per provider (see PinsProvider);
//   - Server Components never read or write it: it only exists in the browser.
import { createJSONStorage, persist } from 'zustand/middleware'
import { createStore } from 'zustand/vanilla'

export type Pin = { stokvelId: string; name: string }

export type PinsState = {
  pins: Pin[]
  /** False until the pins saved in this browser have been read (see PinsProvider). */
  ready: boolean
  /** Pins a stokvel, or updates its name if it is already pinned. */
  pin: (pin: Pin) => void
  unpin: (stokvelId: string) => void
  /** Keeps only the pins whose stokvel the member is still in. */
  keepOnly: (stokvelIds: readonly string[]) => void
  clear: () => void
}

/** Pins are saved per person, so the next person on the same browser never finds them. */
const KEY_PREFIX = 'ronditrack-pins'
export const pinsStorageKey = (userId: string) => `${KEY_PREFIX}:${userId}`

/**
 * localStorage that cannot crash the app. A private window, blocked site data or a full disk can make any
 * access throw; every call is wrapped, and a failure just means "nothing saved".
 */
const safeStorage = {
  getItem: (name: string): string | null => {
    try {
      return window.localStorage.getItem(name)
    } catch {
      return null
    }
  },
  setItem: (name: string, value: string): void => {
    try {
      window.localStorage.setItem(name, value)
    } catch {
      /* not saved; the pins still work until the tab closes */
    }
  },
  removeItem: (name: string): void => {
    try {
      window.localStorage.removeItem(name)
    } catch {
      /* nothing to remove */
    }
  },
}

/** Removes every person's saved pins from this browser. Used when the sign-in page opens. */
export function clearAllSavedPins(): void {
  try {
    for (const key of Object.keys(window.localStorage)) {
      if (key.startsWith(`${KEY_PREFIX}:`)) window.localStorage.removeItem(key)
    }
  } catch {
    /* storage is not available: there is nothing saved to clear */
  }
}

/** What was read from storage may have been edited by hand: keep only well-formed pins. */
function sanitize(value: unknown): Pin[] {
  if (!Array.isArray(value)) return []
  const seen = new Set<string>()
  const pins: Pin[] = []
  for (const item of value) {
    if (typeof item?.stokvelId === 'string' && typeof item?.name === 'string' && !seen.has(item.stokvelId)) {
      seen.add(item.stokvelId)
      pins.push({ stokvelId: item.stokvelId, name: item.name })
    }
  }
  return pins
}

export function createPinsStore(userId: string) {
  return createStore<PinsState>()(
    persist(
      (set) => ({
        pins: [],
        ready: false,
        pin: (pin) =>
          set((state) =>
            state.pins.some((p) => p.stokvelId === pin.stokvelId)
              ? { pins: state.pins.map((p) => (p.stokvelId === pin.stokvelId ? pin : p)) }
              : { pins: [...state.pins, pin] },
          ),
        unpin: (stokvelId) => set((state) => ({ pins: state.pins.filter((p) => p.stokvelId !== stokvelId) })),
        keepOnly: (stokvelIds) =>
          set((state) => {
            const kept = state.pins.filter((p) => stokvelIds.includes(p.stokvelId))
            return kept.length === state.pins.length ? state : { pins: kept }
          }),
        clear: () => set({ pins: [] }),
      }),
      {
        name: pinsStorageKey(userId),
        storage: createJSONStorage(() => safeStorage),
        // Do NOT read localStorage while the store is created. The server has no localStorage, so the first
        // render would differ from the server's HTML (a hydration warning). PinsProvider reads it in an effect.
        skipHydration: true,
        partialize: (state) => ({ pins: state.pins }), // only the pins are saved, never `ready` or the functions
        merge: (saved, current) => ({ ...current, pins: sanitize((saved as { pins?: unknown } | undefined)?.pins) }),
      },
    ),
  )
}

/** The store `createPinsStore` makes (with the `persist` helpers on it). */
export type PinsStore = ReturnType<typeof createPinsStore>
