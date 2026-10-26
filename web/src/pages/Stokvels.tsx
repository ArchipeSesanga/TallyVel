import { useHome } from '../api/queries'
import { StokvelCard } from '../components/StokvelCard'

export default function Stokvels() {
  const homeQuery = useHome()
  const home = homeQuery.data ?? []

  return (
    <section>
      <h2 className="mb-4 text-lg font-semibold">My stokvels</h2>
      {homeQuery.isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
      {homeQuery.isError && (
        <p role="alert" className="text-sm text-red-600">Could not load your stokvels.</p>
      )}
      {homeQuery.isSuccess && home.length === 0 && (
        <p className="text-sm text-muted-foreground">You are not in any stokvel yet.</p>
      )}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {home.map((entry) => (
          // The key is the database id, not the array index.
          <StokvelCard key={entry.stokvelId} entry={entry} />
        ))}
      </div>
    </section>
  )
}
