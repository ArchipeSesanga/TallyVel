'use server'

import { redirect } from 'next/navigation'
import { api, correlationIdOf } from '@/lib/api'
import { createSession, deleteSession, getSession } from '@/lib/session'
import { describeApiError } from '@/lib/errorMessages'
import { safeNextPath } from '@/lib/safeNext'
import { refreshCookieHeader, refreshTokenFrom } from '@/lib/refreshCookie'

export type SignInState = { message: string | null; reference: string | null }

export async function signIn(_previous: SignInState, formData: FormData): Promise<SignInState> {
  const email = String(formData.get('email') ?? '')
  const password = String(formData.get('password') ?? '')

  // null = no answer at all (API down or network lost)
  const login = await api.POST('/api/v1/auth/login', { body: { email, password } }).catch(() => null)
  if (login === null) return { message: "Can't reach RondiTrack. Check your connection.", reference: null }

  if (login.data === undefined) {
    const message =
      login.response.status === 401
        ? 'Email or password is incorrect.' // never say which one was wrong
        : describeApiError(login.response.status, login.response.headers.get('Retry-After'))
    return { message, reference: correlationIdOf(login.error) }
  }

  const refreshToken = refreshTokenFrom(login.response)
  if (refreshToken === null) return { message: 'Sign-in failed. Please try again.', reference: null }

  const me = await api.GET('/api/v1/me', {
    headers: { Authorization: `Bearer ${login.data.accessToken}` },
  })
  if (me.data === undefined) {
    return { message: describeApiError(me.response.status, null), reference: correlationIdOf(me.error) }
  }

  await createSession({
    accessToken: login.data.accessToken,
    accessTokenExpiresAt: login.data.expiresAt,
    refreshToken,
    user: { id: me.data.id, email: me.data.email, fullName: me.data.fullName },
  })
  redirect(safeNextPath(String(formData.get('next') ?? '') || null))
}

export type RegisterState = {
  message: string | null
  reference: string | null
  fieldErrors: Record<string, string[]>
  // What the person typed (never the password), so the form can keep it after an error.
  values: { email: string; fullName: string }
}

export async function register(_previous: RegisterState, formData: FormData): Promise<RegisterState> {
  const email = String(formData.get('email') ?? '')
  const fullName = String(formData.get('fullName') ?? '')
  const password = String(formData.get('password') ?? '')
  const values = { email, fullName }

  const result = await api.POST('/api/v1/auth/register', { body: { email, password, fullName } }).catch(() => null)
  if (result === null) {
    return { message: "Can't reach RondiTrack. Check your connection.", reference: null, fieldErrors: {}, values }
  }

  if (result.data === undefined) {
    const status = result.response.status
    const reference = correlationIdOf(result.error)

    // 400: the API's message for each field goes next to that field.
    if (status === 400 && typeof result.error === 'object' && result.error !== null && 'errors' in result.error) {
      const errors = (result.error as { errors?: Record<string, string[]> }).errors ?? {}
      return { message: null, reference, fieldErrors: errors, values }
    }

    // 409: the email already has an account. We don't say so, because that would tell anyone
    // who has an account (account enumeration). See README, API check question 3.
    if (status === 409) {
      return {
        message: "We couldn't create an account with those details. If you already have one, sign in.",
        reference,
        fieldErrors: {},
        values,
      }
    }

    return { message: describeApiError(status, result.response.headers.get('Retry-After')), reference, fieldErrors: {}, values }
  }

  // Registered. The API doesn't sign people in, so send them to sign in (README: Sessions and refresh).
  redirect('/login?registered=1')
}

export async function signOut(): Promise<void> {
  const session = await getSession()
  if (session !== null) {
    // The API reads the refresh token from its own cookie, so send it the way a browser would.
    await fetch(`${process.env.API_BASE_URL}/api/v1/auth/logout`, {
      method: 'POST',
      headers: { Cookie: refreshCookieHeader(session.refreshToken) },
    }).catch(() => undefined) // sign out locally even if the API is unreachable
  }
  await deleteSession()
  redirect('/login')
}