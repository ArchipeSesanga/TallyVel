import { Suspense } from 'react'
import { redirect } from 'next/navigation'
import { AppShell } from '@/components/AppShell'
import { QueryProvider } from '@/components/QueryProvider'
import { loginPath } from '@/lib/loginPath'
import { api } from '@/lib/api'
import { getSession } from '@/lib/session'

/** Reads the session, so it sits inside <Suspense>: the frame shows straight away without it. */
async function SignedInShell({ children }: { children: React.ReactNode }) {
  const session = await getSession()
  if (session === null) redirect(await loginPath())
  // The stokvels this member is in right now, so a pin for one they LEFT can be hidden. If the API can't
  // be reached we pass null and hide nothing; the layout never fails because of this.
  const home = await api
    .GET('/api/v1/me/home', { headers: { Authorization: `Bearer ${session.accessToken}` } })
    .catch(() => null)
  const memberOf = home?.data ? home.data.stokvels.map((s) => s.stokvelId) : null

  // Only the name, email, id and stokvel ids cross into the browser, never the session object or a token.
  return (
    <AppShell fullName={session.user.fullName} email={session.user.email} userId={session.user.id} memberOf={memberOf}>
      {children}
    </AppShell>
  )
}

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    // The TanStack Query cache lives here, for the signed-in part of the site only.
    <QueryProvider>
      {/* While the session loads, the same frame shows with no name yet. */}
      <Suspense fallback={<AppShell fullName="" email="" userId="" memberOf={null}>{children}</AppShell>}>
        <SignedInShell>{children}</SignedInShell>
      </Suspense>
    </QueryProvider>
  )
}
