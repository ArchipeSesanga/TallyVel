// The rules of "record a contribution", as plain code with no Next.js in it, so tests can run the REAL rules with
// a fake API behind them. The Server Action in app/actions.ts only supplies the three real things (the signed-in
// person's request to the API, a new random key, and refreshing the page) through a RecordPort.
//
// WHY THE IDEMPOTENCY KEY IS MADE HERE (and not in the form): a key must be the same on a retry and different for the
// next payment. Only the code that sees the API's answer knows which of the two this is (no answer or a 5xx: the
// payment may have gone through, so retry with the SAME key; a recorded payment: the next one is NEW). The form only
// carries whatever key the last answer told it to send.

import { z } from 'zod'
import { contributionSchema } from './contributionSchema'
import { describeApiError, tryAgainIn } from './errorMessages'
import { parseRandsToCents } from './moneyInput'

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

/** What the API said, boiled down to what these rules need. */
export type ApiAnswer = {
  status: number
  /** True when the contribution was recorded (201, or a replay of an earlier 201). */
  recorded: boolean
  /** The problem+json body, if any. */
  problem: unknown
  correlationId: string | null
  retryAfter: string | null
}

export type RecordPort = {
  /** Sends the request. Resolves to null when there was no answer at all (the connection dropped). */
  send(request: {
    stokvelId: string
    memberUserId: string
    cycle: string
    amountCents: number
    idempotencyKey: string
  }): Promise<ApiAnswer | null>
  /** A new random UUID. */
  newKey(): string
  /** Called once a contribution is recorded, so the page's totals are drawn again. */
  onRecorded(): void
}

/** Thrown when the API says the session is no longer valid (401); the Server Action turns it into "sign in again". */
export class SessionExpiredError extends Error {}

export const MAY_HAVE_GONE_THROUGH = 'This payment may have gone through. Trying again is safe.'

/** A field the API names in a 400 → the form field that shows it. (The API says "amountCents"; the form's field is "amount".) */
const API_FIELD_TO_FORM_FIELD: Record<string, string> = { amountCents: 'amount', memberUserId: 'memberUserId', cycle: 'cycle' }

/** What the API's problem+json says, or nothing. */
export function problemOf(error: unknown): { detail: string | null; errors: Record<string, string[]> } {
  if (typeof error !== 'object' || error === null) return { detail: null, errors: {} }
  const e = error as { detail?: unknown; errors?: unknown }
  return {
    detail: typeof e.detail === 'string' ? e.detail : null,
    errors: typeof e.errors === 'object' && e.errors !== null ? (e.errors as Record<string, string[]>) : {},
  }
}

/**
 * Which key does this attempt send?
 * - the key the form carries, if it is a real UUID (otherwise a fresh one);
 * - but a NEW one if the person changed the member or the amount since that key was last sent, because the API
 *   answers 422 to a reused key with a different body.
 */
export function chooseKey(
  keyInForm: string,
  previousKeyBody: ContributionState['keyBody'],
  body: { memberUserId: string; amountCents: number },
  newKey: () => string,
): string {
  const bodyChanged =
    previousKeyBody !== null && (previousKeyBody.memberUserId !== body.memberUserId || previousKeyBody.amountCents !== body.amountCents)
  const keyIsUsable = z.uuid().safeParse(keyInForm).success
  return keyIsUsable && !bodyChanged ? keyInForm : newKey()
}

/** Turns a refusal from the API into what the form shows. `again` is the state to return, with the key kept for a retry. */
export function stateFromRefusal(answer: ApiAnswer, again: (message: string | null, fields?: Record<string, string[]>) => ContributionState): ContributionState {
  const problem = problemOf(answer.problem)
  const { status } = answer

  if (status === 400) {
    const fields: Record<string, string[]> = {}
    for (const [apiField, messages] of Object.entries(problem.errors)) {
      const formField = API_FIELD_TO_FORM_FIELD[apiField] ?? 'form'
      fields[formField] = [...(fields[formField] ?? []), ...messages]
    }
    const stray = fields.form ?? []
    delete fields.form
    return again(stray.length > 0 ? stray.join(' ') : problem.detail, fields)
  }
  // Refused for what was asked, not for how it was asked: the API released the key, so the same one is fine.
  if (status === 409 || status === 422) return again(problem.detail ?? describeApiError(status, null))
  if (status === 403) return again(problem.detail ?? describeApiError(403, null)) // a refusal, not a sign-out
  if (status === 404) return again("We couldn't find that stokvel, or you are not a member of it.")
  if (status === 429) return again(tryAgainIn(answer.retryAfter))
  if (status >= 500) return again(MAY_HAVE_GONE_THROUGH)
  return again(describeApiError(status, null))
}

export async function recordContributionCore(previous: ContributionState, formData: FormData, port: RecordPort): Promise<ContributionState> {
  const raw = {
    stokvelId: String(formData.get('stokvelId') ?? ''),
    memberUserId: String(formData.get('memberUserId') ?? ''),
    cycle: String(formData.get('cycle') ?? ''),
    amount: String(formData.get('amount') ?? ''),
    idempotencyKey: String(formData.get('idempotencyKey') ?? ''),
  }
  const values = { memberUserId: raw.memberUserId, amount: raw.amount }
  const keyInForm = z.uuid().safeParse(raw.idempotencyKey).success ? raw.idempotencyKey : port.newKey()

  // The state that comes back when the request didn't reach the API: nothing was sent, so the key is untouched.
  const refused = (fields: Record<string, string[]>, message: string | null): ContributionState => ({
    ...previous, message, reference: null, fieldErrors: fields, values, idempotencyKey: keyInForm, confirmation: null,
  })

  // 1. Zod: the shape of what was typed.
  const parsed = contributionSchema.safeParse(raw)
  if (!parsed.success) {
    const fields = z.flattenError(parsed.error).fieldErrors as Record<string, string[]>
    // Fields the person can't edit (hidden ones) are a problem with the page, not with what they typed.
    const hidden = ['stokvelId', 'cycle', 'idempotencyKey'].flatMap((name) => fields[name] ?? [])
    return refused(fields, hidden.length > 0 ? hidden.join(' ') : null)
  }

  const { stokvelId, memberUserId, cycle } = parsed.data
  const amountCents = parseRandsToCents(parsed.data.amount)! // the schema has already proved this is a whole number of cents

  // 2. The key for THIS attempt.
  const idempotencyKey = chooseKey(parsed.data.idempotencyKey, previous.keyBody, { memberUserId, amountCents }, port.newKey)
  const sameKeyNext = { idempotencyKey, keyBody: { memberUserId, amountCents } }

  // 3. The API.
  const answer = await port.send({ stokvelId, memberUserId, cycle, amountCents, idempotencyKey })

  // No answer: it may or may not have been recorded. Keep the key, so retrying can't record twice.
  if (answer === null) {
    return { ...previous, ...sameKeyNext, message: MAY_HAVE_GONE_THROUGH, reference: null, fieldErrors: {}, values, confirmation: null }
  }

  if (answer.status === 401) throw new SessionExpiredError() // a 401 is "sign in again", never "no access"

  // Recorded (a replay of an earlier 201 looks the same). The next payment is a new request: new key.
  if (answer.recorded) {
    port.onRecorded()
    return {
      message: null, reference: null, fieldErrors: {},
      values: { memberUserId, amount: '' },
      idempotencyKey: port.newKey(), keyBody: null,
      confirmation: { memberUserId, amountCents, cycle },
    }
  }

  const failed = (message: string | null, fieldErrors: Record<string, string[]> = {}): ContributionState => ({
    ...previous, ...sameKeyNext, message, reference: answer.correlationId, fieldErrors, values, confirmation: null,
  })
  return stateFromRefusal(answer, failed)
}
