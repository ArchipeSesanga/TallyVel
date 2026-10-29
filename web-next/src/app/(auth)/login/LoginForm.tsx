'use client'

import { useActionState } from 'react'
import { signIn, type SignInState } from '@/app/actions'

const initialState: SignInState = { message: null, reference: null }

const inputClass =
  'w-full rounded-md bg-card px-3 py-2.5 text-sm text-card-foreground outline-1 -outline-offset-1 outline-input placeholder:text-muted-foreground focus:outline-2 focus:-outline-offset-2 focus:outline-ring'

export function LoginForm() {
  const [state, formAction, pending] = useActionState(signIn, initialState)

  return (
    <form action={formAction} className="space-y-6">
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
          className={inputClass}
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
          className={inputClass}
        />
      </div>

      {/* role="alert" makes screen readers read the error out as soon as it appears */}
      {state.message !== null && (
        <p role="alert" className="rounded-md border border-destructive bg-destructive-soft px-3 py-2 text-sm font-medium text-destructive">
          {state.message}
          {state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
        </p>
      )}

      <button
        type="submit"
        disabled={pending}
        className="w-full cursor-pointer rounded-md border border-primary bg-primary px-3.5 py-2 text-sm font-semibold text-primary-foreground transition-all hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60"
      >
        {pending ? 'Signing in…' : 'Sign in'}
      </button>
    </form>
  )
}
