import { Suspense } from 'react'
import type { Metadata } from 'next'
import { AppShell } from '@/components/AppShell'
import { QueryProvider } from '@/components/QueryProvider'
import { SignedInShell } from '@/components/SignedInShell'

// EVERY signed-in page is private: this one line, here, is the whole rule. Pages and groups below do not repeat it,
// and robots.txt does NOT block them (a crawler that cannot fetch a page never reads its noindex).
export const metadata: Metadata = {
  robots: { index: false, follow: false },
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
