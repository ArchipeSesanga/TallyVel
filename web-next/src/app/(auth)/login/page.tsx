import { LoginForm } from './LoginForm'

export default async function LoginPage({ searchParams }: PageProps<'/login'>) {
  // searchParams is a Promise in Next.js 16, so it has to be awaited.
  const { expired } = await searchParams

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
          {expired !== undefined && (
            <p role="status" className="mb-6 text-sm text-muted-foreground">
              Your session ended. Please sign in again.
            </p>
          )}
          <LoginForm />
        </div>
      </div>
    </main>
  )
}
