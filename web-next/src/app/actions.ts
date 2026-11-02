'use server'

import { refresh } from 'next/cache'
import { redirect } from 'next/navigation'
import { z } from 'zod'
import { api, correlationIdOf } from '@/lib/api'
import { createSession, deleteSession, getSession } from '@/lib/session'
import { describeApiError } from '@/lib/errorMessages'
import { safeNextPath } from '@/lib/safeNext'
import { refreshCookieHeader, refreshTokenFrom } from '@/lib/refreshCookie'
import { contributionSchema } from '@/lib/contributionSchema'
import { loginPath } from '@/lib/loginPath'
import { parseRandsToCents } from '@/lib/moneyInput'

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

export type ContributionState = {
  /** Text for the top of the form: a refusal (409, 422, 403), a wait (429) or "may have gone through". */
  message: string | null
  /** The API's correlation id for a failed request, so the person can quote it. */
  reference: string | null
  /** Messages under each field (400 from the API, or the Zod check). */
  fieldErrors: Record<string, string[]>
  /** What the person typed. React resets an action form, so the form shows these as defaultValue. */
  values: { memberUserId: string; amount: string }
  /** The key the form sends next. The server writes it; the form carries it in a hidden field. */
  idempotencyKey: string
  /** The body the current key was last sent with, or null if the key hasn't been used. Changing the body means a new key. */
  keyBody: { memberUserId: string; amountCents: number } | null
  /** Set after a recorded contribution (a replay counts), so the form can say who, how much and which cycle. */
  confirmation: { memberUserId: string; amountCents: number; cycle: string } | null
}

/** A field the API names in a 400 → the form field that shows it. */
const API_FIELD_TO_FORM_FIELD: Record<string, string> = { amountCents: 'amount', memberUserId: 'memberUserId', cycle: 'cycle' }

/** What the API's problem+json says, or nothing. */
function problemOf(error: unknown): { detail: string | null; errors: Record<string, string[]> } {
  if (typeof error !== 'object' || error === null) return { detail: null, errors: {} }
  const e = error as { detail?: unknown; errors?: unknown }
  return {
    detail: typeof e.detail === 'string' ? e.detail : null,
    errors: typeof e.errors === 'object' && e.errors !== null ? (e.errors as Record<string, string[]>) : {},
  }
}

const MAY_HAVE_GONE_THROUGH = 'This payment may have gone through. Trying again is safe.'

export async function recordContribution(previous: ContributionState, formData: FormData): Promise<ContributionState> {
  // 1. Session. (proxy.ts has already refreshed it if it was nearly used up.)
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const raw = {
    stokvelId: String(formData.get('stokvelId') ?? ''),
    memberUserId: String(formData.get('memberUserId') ?? ''),
    cycle: String(formData.get('cycle') ?? ''),
    amount: String(formData.get('amount') ?? ''),
    idempotencyKey: String(formData.get('idempotencyKey') ?? ''),
  }
  const values = { memberUserId: raw.memberUserId, amount: raw.amount }
  const keyInForm = z.uuid().safeParse(raw.idempotencyKey).success ? raw.idempotencyKey : crypto.randomUUID()

  // The state that comes back when the request didn't reach the API: nothing was sent, so the key is untouched.
  const refused = (fields: Record<string, string[]>, message: string | null): ContributionState => ({
    ...previous, message, reference: null, fieldErrors: fields, values, idempotencyKey: keyInForm, confirmation: null,
  })

  // 2. Zod: the shape of what was typed.
  const parsed = contributionSchema.safeParse(raw)
  if (!parsed.success) {
    const fields = z.flattenError(parsed.error).fieldErrors as Record<string, string[]>
    // Fields the person can't edit (hidden ones) are a problem with the page, not with what they typed.
    const hidden = ['stokvelId', 'cycle', 'idempotencyKey'].flatMap((name) => fields[name] ?? [])
    return refused(fields, hidden.length > 0 ? hidden.join(' ') : null)
  }

  const { stokvelId, memberUserId, cycle } = parsed.data
  const amountCents = parseRandsToCents(parsed.data.amount)! // the schema has already proved this is a whole number of cents

  // The key: a new one if the person changed the amount or the member since this key was last sent,
  // because the API answers 422 to a reused key with a different body.
  const bodyChanged =
    previous.keyBody !== null && (previous.keyBody.memberUserId !== memberUserId || previous.keyBody.amountCents !== amountCents)
  const idempotencyKey = bodyChanged ? crypto.randomUUID() : parsed.data.idempotencyKey
  const sameKeyNext = { idempotencyKey, keyBody: { memberUserId, amountCents } }

  // 3. The API.
  const result = await api
    .POST('/api/v1/stokvels/{stokvelId}/contributions', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: { stokvelId }, header: { 'Idempotency-Key': idempotencyKey } },
      body: { memberUserId, cycle, amountCents },
    })
    .catch(() => null)

  // No answer: it may or may not have been recorded. Keep the key, so retrying can't record twice.
  if (result === null) {
    return { ...previous, ...sameKeyNext, message: MAY_HAVE_GONE_THROUGH, reference: null, fieldErrors: {}, values, confirmation: null }
  }

  const status = result.response.status
  if (status === 401) redirect(await loginPath({ expired: true })) // a 401 is "sign in again", never "no access"

  // Recorded (a replay of an earlier 201 looks the same). The next payment is a new request: new key.
  if (result.data !== undefined) {
    // Re-render this page's data now, so the cycle's total changes without a reload. Without this call
    // the total stayed at its old value until the person reloaded (checked: README, "Record a contribution").
    refresh()
    return {
      message: null, reference: null, fieldErrors: {},
      values: { memberUserId, amount: '' },
      idempotencyKey: crypto.randomUUID(), keyBody: null,
      confirmation: { memberUserId, amountCents, cycle },
    }
  }

  const problem = problemOf(result.error)
  const reference = correlationIdOf(result.error)
  const failed = (message: string | null, fieldErrors: Record<string, string[]> = {}): ContributionState => ({
    ...previous, ...sameKeyNext, message, reference, fieldErrors, values, confirmation: null,
  })

  if (status === 400) {
    const fields: Record<string, string[]> = {}
    for (const [apiField, messages] of Object.entries(problem.errors)) {
      const formField = API_FIELD_TO_FORM_FIELD[apiField] ?? 'form'
      fields[formField] = [...(fields[formField] ?? []), ...messages]
    }
    const stray = fields.form ?? []
    delete fields.form
    return failed(stray.length > 0 ? stray.join(' ') : problem.detail, fields)
  }
  // Refused for what was asked, not for how it was asked: the API released the key, so the same one is fine.
  if (status === 409 || status === 422) return failed(problem.detail ?? describeApiError(status, null))
  if (status === 403) return failed(problem.detail ?? describeApiError(403, null)) // a refusal, not a sign-out
  if (status === 404) return failed("We couldn't find that stokvel, or you are not a member of it.")
  if (status === 429) {
    const seconds = Math.ceil(Number(result.response.headers.get('Retry-After')))
    return failed(Number.isFinite(seconds) && seconds > 0 ? `Try again in ${seconds} seconds.` : 'Try again in a moment.')
  }
  if (status >= 500) return failed(MAY_HAVE_GONE_THROUGH)
  return failed(describeApiError(status, null))
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