import { redirect } from 'next/navigation'
import { AppShell } from '@/components/AppShell'
import { loginPath } from '@/lib/loginPath'
import { api } from '@/lib/api'
import { getSession } from '@/lib/session'

/**
 * The signed-in frame (sidebar, header, pins) around a page, for a person who has a session. It reads the session, so
 * it must sit inside <Suspense>: the frame then shows straight away instead of waiting.
 *
 * `whenSignedOut` is what happens without a session. The signed-in part of the site sends the person to /login (the
 * default). The invite page is the one exception: it must still answer a request that is not signed in (see join/layout.tsx).
 */
export async function SignedInShell({
  children, whenSignedOut = 'login',
}: { children: React.ReactNode; whenSignedOut?: 'login' | 'bare' }) {
  const session = await getSession()
  if (session === null) {
    if (whenSignedOut === 'bare') return <>{children}</>
    redirect(await loginPath())
  }
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
