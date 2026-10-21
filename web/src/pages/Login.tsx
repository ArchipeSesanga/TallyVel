import { useState, type FormEvent } from 'react'
import { ApiError, login } from '../auth/session'

/** What a member sees when sign-in fails. Never says WHICH of email/password was wrong. */
function signInErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return 'Email or password is incorrect.'
    if (error.status === 429) {
      const seconds = Number(error.retryAfter)
      return Number.isFinite(seconds)
        ? `Too many sign-in attempts. Try again in ${seconds} seconds.`
        : 'Too many sign-in attempts. Try again in a moment.'
    }
    return 'Something went wrong signing you in. Please try again.'
  }
  // fetch itself failed: no response at all, so the API is down or the network is gone.
  return "Can't reach RondiTrack. Check your connection."
}

type LoginProps = {
  onSignedIn: () => void
}

export default function Login({ onSignedIn }: LoginProps) {
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault() // stop the browser from reloading the page
    const form = new FormData(event.currentTarget)

    setSubmitting(true)
    setError(null)
    try {
      await login(String(form.get('email')), String(form.get('password')))
      onSignedIn()
    } catch (err) {
      setError(signInErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

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

          <form className="space-y-6" onSubmit={handleSubmit}>
            <div>
              <label htmlFor="email" className="mb-2 inline-block text-sm font-medium">
                Email
              </label>
              <input
                type="email"
                id="email"
                name="email"
                autoComplete="email"
                placeholder="you@example.com"
                required
                className="w-full rounded-md bg-card px-3 py-2.5 text-sm text-card-foreground outline-1 -outline-offset-1 outline-input placeholder:text-muted-foreground focus:outline-2 focus:-outline-offset-2 focus:outline-ring"
              />
            </div>

            <div>
              <label htmlFor="password" className="mb-2 inline-block text-sm font-medium">
                Password
              </label>
              <input
                type="password"
                id="password"
                name="password"
                autoComplete="current-password"
                placeholder="••••••••"
                required
                className="w-full rounded-md bg-card px-3 py-2.5 text-sm text-card-foreground outline-1 -outline-offset-1 outline-input placeholder:text-muted-foreground focus:outline-2 focus:-outline-offset-2 focus:outline-ring"
              />
            </div>

            {/* role="alert" makes screen readers read the error out as soon as it appears */}
            {error !== null && (
              <p role="alert" className="rounded-md border border-destructive bg-destructive-soft px-3 py-2 text-sm font-medium text-destructive">
                {error}
              </p>
            )}

            <button
              type="submit"
              disabled={submitting}
              className="w-full cursor-pointer rounded-md border border-primary bg-primary px-3.5 py-2 text-sm font-semibold text-primary-foreground transition-all hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60"
            >
              {submitting ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
        </div>
      </div>
    </main>
  )
}