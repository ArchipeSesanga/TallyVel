import { Suspense } from 'react'
import { AppShell } from '@/components/AppShell'
import { QueryProvider } from '@/components/QueryProvider'
import { SignedInShell } from '@/components/SignedInShell'

// The invite page is NOT in the (app) group, on purpose. Everything in (app) sends a signed-out request to /login, and
// a link-preview bot (WhatsApp, Telegram, Slack...) is never signed in: it would be bounced to the sign-in page and
// would preview THAT. Here a signed-out request gets the page itself, with a head that says nothing about the stokvel.
// A signed-in person gets exactly the same frame as every other signed-in page.
export default function JoinLayout({ children }: { children: React.ReactNode }) {
  return (
    <QueryProvider>
      <Suspense fallback={<AppShell fullName="" email="" userId="" memberOf={null}>{children}</AppShell>}>
        <SignedInShell whenSignedOut="bare">{children}</SignedInShell>
      </Suspense>
    </QueryProvider>
  )
}
