'use server'

import { revalidatePath } from 'next/cache'
import { redirect } from 'next/navigation'
import { z } from 'zod'
import { api, correlationIdOf } from '@/lib/api'
import { getSession } from '@/lib/session'
import { loginPath } from '@/lib/loginPath'
import { describeApiError, localReference, tryAgainIn } from '@/lib/errorMessages'
import { parseRandsToCents } from '@/lib/moneyInput'
import { createStokvelSchema } from '@/lib/stokvelSchema'
import { isUuid } from '@/lib/ids'

export type CreateStokvelState = {
  message: string | null
  reference: string | null
  fieldErrors: Record<string, string[]>
  /** What the person typed, so the form can show it again after an error (React empties an action form). */
  values: { name: string; amount: string; frequency: string; rules: string }
}

/** The API names a field in a 400 ("Name", "ContributionAmountCents"...) → the form field that shows it. */
const API_FIELD_TO_FORM_FIELD: Record<string, string> = { name: 'name', contributionamountcents: 'amount', cycle: 'frequency', rules: 'rules' }

export async function createStokvel(_previous: CreateStokvelState, formData: FormData): Promise<CreateStokvelState> {
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const values = {
    name: String(formData.get('name') ?? ''),
    amount: String(formData.get('amount') ?? ''),
    frequency: String(formData.get('frequency') ?? ''),
    rules: String(formData.get('rules') ?? ''),
  }
  const refused = (fieldErrors: Record<string, string[]>, message: string | null, reference: string | null = null): CreateStokvelState => ({
    message, reference, fieldErrors, values,
  })

  const parsed = createStokvelSchema.safeParse(values)
  if (!parsed.success) return refused(z.flattenError(parsed.error).fieldErrors as Record<string, string[]>, null)

  const result = await api
    .POST('/api/v1/stokvels', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      body: {
        name: parsed.data.name,
        contributionAmountCents: parseRandsToCents(parsed.data.amount)!, // the schema has proved this is whole cents
        cycle: parsed.data.frequency,
        // The API only accepts the caller's own id here (anything else is a 403), so it comes from the session, never the form.
        creatorId: session.user.id,
        rules: parsed.data.rules === '' ? null : parsed.data.rules,
      },
    })
    .catch((error: unknown) => {
      console.error('createStokvel: the API could not be reached', error)
      return null
    })

  if (result === null) return refused({}, "Can't reach RondiTrack. Check your connection and try again.", localReference())

  const { data, error, response } = result
  if (data !== undefined) {
    revalidatePath('/', 'layout') // the shell's list of "my stokvels" has one more
    redirect(`/stokvels/${data.id}?created=1`)
  }

  const status = response.status
  const reference = correlationIdOf(error)
  if (status === 401) redirect(await loginPath({ expired: true }))
  if (status === 400) {
    const errors = (error as { errors?: Record<string, string[]> } | undefined)?.errors ?? {}
    const fields: Record<string, string[]> = {}
    for (const [apiField, messages] of Object.entries(errors)) {
      const formField = API_FIELD_TO_FORM_FIELD[apiField.toLowerCase()] ?? 'form'
      fields[formField] = [...(fields[formField] ?? []), ...messages]
    }
    const stray = fields.form ?? []
    delete fields.form
    return refused(fields, stray.length > 0 ? stray.join(' ') : null, reference)
  }
  if (status === 429) return refused({}, tryAgainIn(response.headers.get('Retry-After')), reference)
  return refused({}, describeApiError(status, null), reference)
}

export type LeaveState = { message: string | null; reference: string | null }

/**
 * "Leave <stokvel>". The person removes THEMSELVES: the id comes from the session, never from the form, so this
 * action cannot be used to remove somebody else. The API refuses the last Chairperson (422) and keeps the
 * person's past contributions.
 */
export async function leaveStokvel(_previous: LeaveState, formData: FormData): Promise<LeaveState> {
  const session = await getSession()
  if (session === null) redirect(await loginPath())

  const stokvelId = String(formData.get('stokvelId') ?? '')
  if (!isUuid(stokvelId)) return { message: "We couldn't find that stokvel.", reference: null }

  const result = await api
    .DELETE('/api/v1/stokvels/{id}/members/{userId}', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: { id: stokvelId, userId: session.user.id } },
    })
    .catch((error: unknown) => {
      console.error('leaveStokvel: the API could not be reached', error)
      return null
    })

  if (result === null) {
    return { message: "We couldn't confirm that you left. Press the button again: trying again is safe.", reference: localReference() }
  }

  const { data, error, response } = result
  const status = response.status
  if (status === 401) redirect(await loginPath({ expired: true }))

  // 404 = already not a member (a retry after the first one worked): the person's goal is met either way.
  if (data !== undefined || status === 404) {
    revalidatePath('/', 'layout')
    redirect('/stokvels?left=1')
  }

  const detail = typeof (error as { detail?: unknown } | undefined)?.detail === 'string' ? (error as { detail: string }).detail : null
  const reference = correlationIdOf(error)
  if (status === 422) return { message: detail ?? "You can't leave because you are the only Chairperson.", reference }
  if (status === 429) return { message: tryAgainIn(response.headers.get('Retry-After')), reference }
  if (status >= 500) return { message: "We couldn't confirm that you left. Press the button again: trying again is safe.", reference }
  return { message: detail ?? describeApiError(status, null), reference }
}
