'use client'

import { useActionState } from 'react'
import { signIn, type SignInState } from '@/app/actions'

const initialState: SignInState = { message: null, reference: null }

export function LoginForm() {
  const [state, formAction, pending] = useActionState(signIn, initialState)

  return (
    <form action={formAction} className="space-y-4">
      <label className="block text-sm font-medium">
        Email
        <input name="email" type="email" autoComplete="email" required className="mt-1 w-full rounded-md border border-input px-3 py-2" />
      </label>
      <label className="block text-sm font-medium">
        Password
        <input name="password" type="password" autoComplete="current-password" required className="mt-1 w-full rounded-md border border-input px-3 py-2" />
      </label>

      {state.message !== null && (
        <p role="alert" className="text-sm text-destructive">
          {state.message}
          {state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
        </p>
      )}

      <button type="submit" disabled={pending} className="w-full rounded-md bg-primary px-3 py-2 text-primary-foreground disabled:opacity-60">
        {pending ? 'Signing in…' : 'Sign in'}
      </button>
    </form>
  )
}