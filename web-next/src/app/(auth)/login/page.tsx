import { LoginForm } from './LoginForm'

export default async function LoginPage({ searchParams }: PageProps<'/login'>) {
  // searchParams is a Promise in Next.js 16, so it has to be awaited.
  const { expired } = await searchParams

  return (
    <main className="mx-auto max-w-sm space-y-4 p-6">
      <h1 className="text-2xl font-bold">Sign in to RondiTrack</h1>
      {expired !== undefined && (
        <p role="status" className="text-sm text-muted-foreground">
          Your session ended. Please sign in again.
        </p>
      )}
      <LoginForm />
    </main>
  )
}
