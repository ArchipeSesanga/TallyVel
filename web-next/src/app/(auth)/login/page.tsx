import { Suspense } from 'react'
import Link from 'next/link'
import { LoginForm } from './LoginForm'

/**
 * Reads ?expired=1, ?registered=1 and ?next=. searchParams is a Promise in Next.js 16,
 * so it is awaited here, inside <Suspense>, and the rest of the page stays static.
 */
async function LoginPanel({ searchParams }: { searchParams: PageProps<'/login'>['searchParams'] }) {
  const { expired, registered, next } = await searchParams

  const notice =
    registered !== undefined
      ? 'Your account was created. Please sign in.'
      : expired !== undefined
        ? 'Your session ended. Please sign in again.'
        : null

  return (
    <>
      {notice !== null && (
        <p role="status" className="mb-6 text-sm text-muted-foreground">
          {notice}
        </p>
      )}
      {/* Only a plain string crosses into the Client Component. The Server Action checks it
          again with safeNextPath, because anything coming back from a form can be changed. */}
      <LoginForm next={typeof next === 'string' ? next : ''} />
    </>
  )
}

/** Same height as the form, so the page doesn't jump when the real form appears. */
function LoginFormSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true" aria-label="Loading the sign-in form">
      <div className="h-16 animate-pulse rounded bg-muted" />
      <div className="h-16 animate-pulse rounded bg-muted" />
      <div className="h-10 animate-pulse rounded bg-muted" />
    </div>
  )
}

export default function LoginPage({ searchParams }: PageProps<'/login'>) {
  return (
    <main className="flex flex-col items-center justify-center bg-background px-4 py-4 text-foreground md:px-8 lg:min-h-screen">
      <div className="grid max-w-lg items-center gap-12 lg:max-w-6xl lg:grid-cols-2">
        <div>
          <p className="text-3xl font-bold !leading-tight sm:text-4xl lg:text-5xl">
            Keep your stokvel on track, together.
          </p>
          <p className="mt-6 text-base leading-relaxed text-muted-foreground">
            Log contributions, see who has paid, and share a payout rotation everyone can trust. No more
            spreadsheets or WhatsApp screenshots.
          </p>
        </div>

        <div className="w-full max-w-md border-t border-border pt-12 lg:ml-auto lg:flex lg:flex-col lg:justify-center lg:self-stretch lg:border-t-0 lg:border-l lg:pt-0 lg:pl-12">
          <h1 className="mb-10 text-3xl font-bold">Sign in</h1>
          <Suspense fallback={<LoginFormSkeleton />}>
            <LoginPanel searchParams={searchParams} />
          </Suspense>
          <p className="mt-6 text-sm">
            New to RondiTrack? <Link href="/register" className="underline">Create an account</Link>
          </p>
        </div>
      </div>
    </main>
  )
}
