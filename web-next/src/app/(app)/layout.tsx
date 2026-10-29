import Link from 'next/link'
import { Suspense } from 'react'
import { redirect } from 'next/navigation'
import { signOut } from '@/app/actions'
import { getSession } from '@/lib/session'

/** Reads the session, so it sits inside <Suspense>: the sidebar shows straight away. */
async function SignedInAs() {
  const session = await getSession()
  if (session === null) redirect('/login')
  // Only the name crosses into the HTML, never the session object.
  return <p className="text-sm text-muted-foreground">Signed in as {session.user.fullName}</p>
}

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen md:grid md:grid-cols-[14rem_1fr]">
      <aside className="space-y-6 border-b border-border p-4 md:border-r md:border-b-0">
        <p className="text-lg font-bold">RondiTrack</p>
        <nav className="flex flex-col gap-2 text-sm">
          <Link href="/dashboard/member">Member dashboard</Link>
          <Link href="/dashboard/treasurer">Treasurer dashboard</Link>
          <Link href="/stokvels">My stokvels</Link>
        </nav>
        <Suspense fallback={<p className="text-sm text-muted-foreground">Loading…</p>}>
          <SignedInAs />
        </Suspense>
        <form action={signOut}>
          <button type="submit" className="text-sm underline">Sign out</button>
        </form>
      </aside>
      <main className="p-4 md:p-8">{children}</main>
    </div>
  )
}