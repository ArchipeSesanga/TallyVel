import { useState } from 'react'
import './App.css'
import Dashboard from './pages/Dashboard'
import Login from './pages/Login'

function App() {
  // Real state: nothing else tells us whether the person has signed in.
  // For now "Sign in" just flips it; a later assignment checks the password first.
  const [signedIn, setSignedIn] = useState(false)

  // Each page brings its own layout, so App adds no wrapper of its own.
  return signedIn ? <Dashboard /> : <Login onSignIn={() => setSignedIn(true)} />
}

export default App
