'use server'

import { redirect } from 'next/navigation'
import { api, correlationIdOf } from '@/lib/api'
import { createSession, deleteSession, getSession } from '@/lib/session'
import { describeApiError } from '@/lib/errorMessages'

export type SignInState = { message: string | null; reference: string | null }

const REFRESH_COOKIE = 'tallyvel_rt='

/** A browser hides Set-Cookie from JavaScript; a server can read it. */
function refreshTokenFrom(response: Response): string | null {
  const cookie = response.headers.getSetCookie().find((c) => c.startsWith(REFRESH_COOKIE))
  return cookie ? cookie.split(';')[0].slice(REFRESH_COOKIE.length) : null
}

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
    refreshToken,
    user: { id: me.data.id, email: me.data.email, fullName: me.data.fullName },
  })
  redirect('/dashboard/member')
}

export async function signOut(): Promise<void> {
  const session = await getSession()
  if (session !== null) {
    // The API reads the refresh token from its own cookie, so send it the way a browser would.
    await fetch(`${process.env.API_BASE_URL}/api/v1/auth/logout`, {
      method: 'POST',
      headers: { Cookie: `tallyvel_rt=${session.refreshToken}` },
    }).catch(() => undefined) // sign out locally even if the API is unreachable
  }
  await deleteSession()
  redirect('/login')
}