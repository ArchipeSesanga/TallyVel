'use client'

import { useActionState } from 'react'
import Link from 'next/link'
import { register, type RegisterState } from '@/app/actions'

const initialState: RegisterState = { message: null, reference: null, fieldErrors: {}, values: { email: '', fullName: '' } }

export function RegisterForm() {
  const [state, formAction, pending] = useActionState(register, initialState)
  const errorsFor = (field: string) => state.fieldErrors[field] ?? []

  return (
    <form action={formAction} className="space-y-4" noValidate>
      {state.message !== null && (
        <p role="alert" className="text-sm text-destructive">
          {state.message}
          {state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
        </p>
      )}

      <div>
        <label htmlFor="fullName" className="block text-sm font-medium">Full name</label>
        <input id="fullName" name="fullName" autoComplete="name" defaultValue={state.values.fullName}
          aria-invalid={errorsFor('fullName').length > 0 ? true : undefined} aria-describedby="fullName-error"
          className="mt-1 w-full rounded-md border border-input px-3 py-2" />
        <p id="fullName-error" className="text-sm text-destructive">{errorsFor('fullName').join(' ')}</p>
      </div>

      <div>
        <label htmlFor="email" className="block text-sm font-medium">Email</label>
        {/* "username" tells password managers this is the account name to save */}
        <input id="email" name="email" type="email" autoComplete="username" defaultValue={state.values.email}
          aria-invalid={errorsFor('email').length > 0 ? true : undefined} aria-describedby="email-error"
          className="mt-1 w-full rounded-md border border-input px-3 py-2" />
        <p id="email-error" className="text-sm text-destructive">{errorsFor('email').join(' ')}</p>
      </div>

      <div>
        <label htmlFor="password" className="block text-sm font-medium">Password</label>
        {/* "new-password" lets the password manager suggest a strong one and save it */}
        <input id="password" name="password" type="password" autoComplete="new-password"
          aria-invalid={errorsFor('password').length > 0 ? true : undefined} aria-describedby="password-hint password-error"
          className="mt-1 w-full rounded-md border border-input px-3 py-2" />
        <p id="password-hint" className="text-sm text-muted-foreground">At least 12 characters.</p>
        <p id="password-error" className="text-sm text-destructive">{errorsFor('password').join(' ')}</p>
      </div>

      <button type="submit" disabled={pending}
        className="w-full rounded-md bg-primary px-3 py-2 text-primary-foreground disabled:opacity-60">
        {pending ? 'Creating your account…' : 'Create account'}
      </button>

      <p className="text-sm">
        Already have an account? <Link href="/login" className="underline">Sign in</Link>
      </p>
    </form>
  )
}