import { Suspense } from 'react'
import { redirect } from 'next/navigation'
import { AppShell } from '@/components/AppShell'
import { getSession } from '@/lib/session'

/** Reads the session, so it sits inside <Suspense>: the frame shows straight away without it. */
async function SignedInShell({ children }: { children: React.ReactNode }) {
  const session = await getSession()
  if (session === null) redirect('/login')
  // Only the name and email cross into the browser, never the session object.
  return (
    <AppShell fullName={session.user.fullName} email={session.user.email}>
      {children}
    </AppShell>
  )
}

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    // While the session loads, the same frame shows with no name yet.
    <Suspense fallback={<AppShell fullName="" email="">{children}</AppShell>}>
      <SignedInShell>{children}</SignedInShell>
    </Suspense>
  )
}
