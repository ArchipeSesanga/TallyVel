'use client'

import { useEffect, useRef } from 'react'

/**
 * The step's heading. When a step appears (a new page load, or Continue/Back), focus moves here, so a screen
 * reader says "Step 2 of 3: Check the rules" and a keyboard user starts at the top of the new step.
 * tabIndex={-1} lets code focus it without adding it to the Tab order. The page gives it key={step}, so each
 * step is a fresh heading and this runs again.
 */
export function StepHeading({ children }: { children: React.ReactNode }) {
  const ref = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    ref.current?.focus()
  }, [])
  return (
    <h1 ref={ref} tabIndex={-1} className="text-2xl font-bold outline-none focus-visible:underline">
      {children}
    </h1>
  )
}
