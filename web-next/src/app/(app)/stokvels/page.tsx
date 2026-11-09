import { Suspense } from 'react'
import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { StokvelCard } from '@/components/StokvelCard'
import { FlashToast } from '@/components/FlashToast'
import { StokvelsHeaderConnected } from '@/components/StokvelsHeaderConnected'
import { groupsFromHome } from '@/lib/groupOverview'

// Not async: returns a <Suspense> instantly, so the page never blocks.
export default function StokvelsPage({ searchParams }: PageProps<'/stokvels'>) {
  return (
    <section className="flex flex-col gap-5">
      {/* After leaving a stokvel the action sends the person here with ?left=1. Reading it needs the request, so: Suspense. */}
      <Suspense fallback={null}>
        <LeftNotice searchParams={searchParams} />
      </Suspense>
      <Suspense fallback={<StokvelsPageSkeleton />}>
        <StokvelsContent />
      </Suspense>
    </section>
  )
}

async function LeftNotice({ searchParams }: { searchParams: PageProps<'/stokvels'>['searchParams'] }) {
  return (await searchParams).left === '1' ? <FlashToast message="You left the stokvel." /> : null
}

// All the waiting (session + API) happens inside the boundary. One request feeds the header and the list.
async function StokvelsContent() {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  if (!home.ok) return <ApiErrorView {...home.problem} />
  const stokvels = home.data.stokvels

  return (
    <>
      <StokvelsHeaderConnected groups={groupsFromHome(stokvels)} />
      {stokvels.length === 0 ? (
        <p>Ask a treasurer for an invite link, or create your own stokvel.</p>
      ) : (
        <ul className="grid gap-4 md:grid-cols-2">
          {stokvels.map((entry) => (
            <li key={entry.stokvelId}>
              <Link href={`/stokvels/${entry.stokvelId}`} className="block rounded-lg focus-visible:ring-2 focus-visible:ring-ring">
                <StokvelCard entry={entry} />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}

// Shaped like the header and the cards, so nothing jumps when they arrive.
function StokvelsPageSkeleton() {
  return (
    <div className="flex flex-col gap-5" aria-busy="true" aria-label="Loading your stokvels">
      <div className="h-20 animate-pulse rounded-xl bg-muted" />
      <ul className="grid gap-4 md:grid-cols-2">
        {[1, 2].map((n) => (
          <li key={n} className="h-48 animate-pulse rounded-lg bg-muted" />
        ))}
      </ul>
    </div>
  )
}
