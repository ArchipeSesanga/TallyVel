type LoginProps = {
  // Called when the form is submitted. For now App just switches to the dashboard;
  // a later assignment will check the email and password here first.
  onSignIn: () => void
}

export default function Login({ onSignIn }: LoginProps) {
  return (
    <main className="flex flex-col items-center justify-center bg-background px-4 py-4 text-foreground md:px-8 lg:min-h-screen">
      <div className="grid max-w-lg items-center gap-12 lg:max-w-6xl lg:grid-cols-2">
        <div>
          <p className="text-4xl font-bold !leading-tight lg:text-5xl">
            Keep your stokvel on track, together.
          </p>
          <p className="mt-6 text-base leading-relaxed text-muted-foreground">
            Log contributions, see who has paid, and share a payout rotation everyone can trust. No more
            spreadsheets or WhatsApp screenshots.
          </p>

          <div className="mt-6 text-sm lg:mt-12">
            Don't have an account?
            <a
              href="#"
              className="ml-1 rounded font-medium text-primary hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
            >
              Register here
            </a>
          </div>
        </div>

        <div className="w-full max-w-md lg:ml-auto">
          <h1 className="mb-10 text-3xl font-bold">Sign in</h1>

          <form
            className="space-y-6"
            onSubmit={(event) => {
              // Stop the browser from reloading the page, which is what a form does by default.
              event.preventDefault()
              onSignIn()
            }}
          >
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

            <button
              type="submit"
              className="w-full cursor-pointer rounded-md border border-primary bg-primary px-3.5 py-2 text-sm font-semibold text-primary-foreground transition-all hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
            >
              Sign in
            </button>
          </form>
        </div>
      </div>
    </main>
  )
}