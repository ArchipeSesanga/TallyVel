import { Suspense } from 'react'
import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { StokvelCard } from '@/components/StokvelCard'
import { FlashToast } from '@/components/FlashToast'

// Not async: returns the heading and a <Suspense> instantly, so the page never blocks.
export default function StokvelsPage({ searchParams }: PageProps<'/stokvels'>) {
  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">My stokvels</h1>
        <span className="flex gap-4 text-sm">
          <Link href="/stokvels/new" className="underline">Create a stokvel</Link>
          <Link href="/join" className="underline">Join with an invite code</Link>
        </span>
      </div>
      {/* After leaving a stokvel the action sends the person here with ?left=1. Reading it needs the request, so: Suspense. */}
      <Suspense fallback={null}>
        <LeftNotice searchParams={searchParams} />
      </Suspense>
      <Suspense fallback={<StokvelCardsSkeleton />}>
        <StokvelList />
      </Suspense>
    </section>
  )
}

async function LeftNotice({ searchParams }: { searchParams: PageProps<'/stokvels'>['searchParams'] }) {
  return (await searchParams).left === '1' ? <FlashToast message="You left the stokvel." /> : null
}

// All the waiting (session + API) happens inside the boundary.
async function StokvelList() {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  if (!home.ok) return <ApiErrorView {...home.problem} />

  const stokvels = home.data.stokvels
  if (stokvels.length === 0) return <p>You&apos;re not in any stokvel yet. Ask a treasurer for an invite link, <Link href="/join" className="underline">enter an invite code</Link>, or <Link href="/stokvels/new" className="underline">create your own stokvel</Link>.</p>

  return (
    <ul className="grid gap-4 md:grid-cols-2">
      {stokvels.map((entry) => (
        <li key={entry.stokvelId}>
          <Link href={`/stokvels/${entry.stokvelId}`} className="block rounded-lg focus-visible:ring-2 focus-visible:ring-ring">
            <StokvelCard entry={entry} />
          </Link>
        </li>
      ))}
    </ul>
  )
}

// Shaped like the real cards, so nothing jumps when they arrive.
function StokvelCardsSkeleton() {
  return (
    <ul className="grid gap-4 md:grid-cols-2" aria-busy="true" aria-label="Loading your stokvels">
      {[1, 2].map((n) => (
        <li key={n} className="h-48 animate-pulse rounded-lg bg-muted" />
      ))}
    </ul>
  )
}