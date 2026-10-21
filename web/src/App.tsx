import { useEffect, useState } from 'react'
import { useContributions, useHome } from './api/queries'
import { queryClient } from './api/queryClient'
import { logout, onSignedOut, refreshAccessToken } from './auth/session'
import { StokvelCard } from './components/StokvelCard'
import { ContributionCard } from './components/ContributionCard'
import Login from './pages/Login'

function Contributions({ stokvelId }: { stokvelId: string }) {
  const contributions = useContributions(stokvelId)

  if (contributions.isPending) return <p>Loading contributions…</p>
  if (contributions.isError) return <p>Couldn't load the contributions.</p>
  if (contributions.data.length === 0) {
    return <p>No contributions yet. They'll appear here once members start paying.</p>
  }

  return (
    <ul className="space-y-3">
      {contributions.data.map((contribution) => (
        <li key={contribution.id}>
          <ContributionCard contribution={contribution} />
        </li>
      ))}
    </ul>
  )
}

function HomeScreen() {
  const home = useHome()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  if (home.isPending) return <p className="p-4">Loading your stokvels…</p>
  if (home.isError) return <p className="p-4">Couldn't load your stokvels.</p>
  if (home.data.length === 0) {
    return <p className="p-4">You're not in any stokvel yet. Ask your treasurer to add you.</p>
  }

  const selected = home.data.find((s) => s.stokvelId === selectedId) ?? home.data[0]

  return (
    <main className="mx-auto max-w-5xl space-y-8 p-4">
      <header className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-bold">Your stokvels</h1>
        <button
          type="button"
          onClick={() => void logout()}
          className="rounded-md border border-border px-3 py-2 text-sm font-medium hover:bg-muted"
        >
          Sign out
        </button>
      </header>

      <section className="grid gap-4 md:grid-cols-2">
        {home.data.map((entry) => (
          <StokvelCard key={entry.stokvelId} entry={entry} />
        ))}
      </section>

      <section className="space-y-4">
        <label className="block text-sm font-medium">
          Show contributions for{' '}
          <select
            value={selected.stokvelId}
            onChange={(e) => setSelectedId(e.target.value)}
            className="ml-2 rounded-md border border-border bg-card px-2 py-1"
          >
            {home.data.map((s) => (
              <option key={s.stokvelId} value={s.stokvelId}>
                {s.name}
              </option>
            ))}
          </select>
        </label>

        <h2 className="text-xl font-semibold">{selected.name}</h2>
        <Contributions stokvelId={selected.stokvelId} />
      </section>
    </main>
  )
}

type SessionStatus = 'checking' | 'signedIn' | 'signedOut'

function App() {
  const [status, setStatus] = useState<SessionStatus>('checking')

  useEffect(() => {
    let cancelled = false

    // On page load the token in memory is gone. Ask the API, using the HttpOnly cookie,
    // whether this browser still has a session.
    refreshAccessToken().then((token) => {
      if (!cancelled) setStatus(token === null ? 'signedOut' : 'signedIn')
    })

    // Signed out by the button OR by a failed refresh: wipe every cached answer, so the
    // next person on a shared phone can't see a single rand of the previous member's data.
    const stopListening = onSignedOut(() => {
      queryClient.clear()
      setStatus('signedOut')
    })

    return () => {
      cancelled = true
      stopListening()
    }
  }, [])

  if (status === 'checking') return <p className="p-4">Loading…</p>
  if (status === 'signedOut') return <Login onSignedIn={() => setStatus('signedIn')} />
  return <HomeScreen />
}

export default App