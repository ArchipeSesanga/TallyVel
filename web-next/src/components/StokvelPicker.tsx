'use client'

import { usePathname, useRouter } from 'next/navigation'

type StokvelPickerProps = {
  stokvels: { stokvelId: string; name: string }[]
  selectedId: string
}

/** Picks which stokvel's contributions to show. The choice lives in the URL (?stokvel=), so the server renders the list. */
export function StokvelPicker({ stokvels, selectedId }: StokvelPickerProps) {
  const router = useRouter()
  const pathname = usePathname()

  return (
    <select
      aria-label="Show contributions for"
      value={selectedId}
      onChange={(e) => router.replace(`${pathname}?stokvel=${encodeURIComponent(e.target.value)}`, { scroll: false })}
      className="rounded-md border border-border bg-card px-2 py-1 text-sm"
    >
      {stokvels.map((s) => (
        <option key={s.stokvelId} value={s.stokvelId}>{s.name}</option>
      ))}
    </select>
  )
}
