import type { Metadata } from 'next'
import { RegisterForm } from './RegisterForm'

// The browser tab and a screen reader's page title. Each page names itself; the site name is added after it.
export const metadata: Metadata = { title: 'Create an account' }

export default function RegisterPage() {
  return (
    <main className="mx-auto max-w-sm space-y-6 p-6">
      <h1 className="text-2xl font-bold">Create your RondiTrack account</h1>
      <RegisterForm />
    </main>
  )
}