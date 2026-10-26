import { useState } from 'react'
import { useContributions, useHome, useMe } from '../api/queries'
import { ContributionCard } from '../components/ContributionCard'
import { ContributionForm } from '../components/ContributionForm'

export default function Contributions() {
  const meQuery = useMe()
  const homeQuery = useHome()
  const home = homeQuery.data ?? []

  // Which stokvel is shown: the first one until the member picks another.
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const chosen = home.find((s) => s.stokvelId === selectedId) ?? home[0]

  const contributionsQuery = useContributions(chosen?.stokvelId ?? '')
  const contributions = contributionsQuery.data ?? []

  if (homeQuery.isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (homeQuery.isError) {
    return <p role="alert" className="text-sm text-red-600">Could not load your stokvels.</p>
  }
  if (!chosen) return <p className="text-sm text-muted-foreground">You are not in any stokvel yet.</p>

  return (
    <section>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">Contributions in {chosen.name}</h2>
        <select
          aria-label="Show contributions for"
          value={chosen.stokvelId}
          onChange={(e) => setSelectedId(e.target.value)}
          className="rounded-md border border-border bg-card px-2 py-1 text-sm"
        >
          {home.map((s) => (
            <option key={s.stokvelId} value={s.stokvelId}>{s.name}</option>
          ))}
        </select>
      </div>

      {/* Only when there is an open cycle to pay into and we know who the member is.
          The key restarts the form (and its prefilled amount) for another stokvel or cycle. */}
      {chosen.currentCycle && meQuery.data && (
        <div className="mb-8 max-w-md">
          <ContributionForm
            key={`${chosen.stokvelId}:${chosen.currentCycle.id}`}
            entry={chosen}
            memberUserId={meQuery.data.id}
            cycleLabel={chosen.currentCycle.label}
          />
        </div>
      )}

      {contributionsQuery.isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
      {contributionsQuery.isError && (
        <p role="alert" className="text-sm text-red-600">Could not load contributions.</p>
      )}
      {contributionsQuery.isSuccess && contributions.length === 0 && (
        <p className="text-sm text-muted-foreground">No contributions yet.</p>
      )}
      <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {contributions.map((c) => (
          <li key={c.id}>
            <ContributionCard contribution={c} />
          </li>
        ))}
      </ul>
    </section>
  )
}
