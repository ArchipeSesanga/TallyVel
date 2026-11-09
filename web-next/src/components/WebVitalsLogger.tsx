'use client'

import { useEffect } from 'react'

/**
 * MEASUREMENT ONLY. Prints Interaction to Next Paint (INP) to the browser console, so it can be read while recording a
 * contribution or opening Close Cycle. It does nothing, and ships no code, unless the app was BUILT with
 *
 *     NEXT_PUBLIC_LOG_WEB_VITALS=1 npm run build && npm run start
 *
 * (A NEXT_PUBLIC_ variable is fixed into the code at build time: without it, the check below is always true, the
 * `import()` after it can never run, and the web-vitals library is left out of the bundle.)
 *
 * To remove it: delete this file and its one line in app/layout.tsx.
 *
 * INP is the slowest interaction (click, tap, key press) seen on the page, from the press until the next frame is
 * painted. `reportAllChanges` prints each time it gets worse, so every press can be watched as it happens. The
 * attribution build also says WHICH element was pressed and where the time went: waiting for the main thread
 * (input delay), running our handlers (processing) or drawing the result (presentation delay).
 */
export function WebVitalsLogger() {
  useEffect(() => {
    if (process.env.NEXT_PUBLIC_LOG_WEB_VITALS !== '1') return
    void import('web-vitals/attribution').then(({ onINP }) => {
      onINP(
        ({ value, rating, attribution }) => {
          console.info(
            `[INP] ${Math.round(value)} ms (${rating}) on ${attribution.interactionTarget} · ${attribution.interactionType}` +
              ` · waiting ${Math.round(attribution.inputDelay)} ms, handlers ${Math.round(attribution.processingDuration)} ms, painting ${Math.round(attribution.presentationDelay)} ms`,
          )
        },
        { reportAllChanges: true },
      )
    })
  }, [])
  return null
}
