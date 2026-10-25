import { useEffect, useState } from 'react'
import { queryClient } from './api/queryClient'
import { onSignedOut, refreshAccessToken } from './auth/session'
import Dashboard from './pages/Dashboard'
import Login from './pages/Login'

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
  return <Dashboard />
}

export default App