import { describe, expect, it } from 'vitest'
import {
  MAY_HAVE_GONE_THROUGH, SessionExpiredError, chooseKey, recordContributionCore,
  type ApiAnswer, type ContributionState, type RecordPort,
} from './recordContribution'

const STOKVEL = '4ce16b73-25dd-4a88-b36c-e8b61a24bd65'
const MEMBER = 'c67160fb-d6f0-40de-96e7-261823fef0f9'
const OTHER = '0b9a7f68-5b5a-4a53-9a43-3c1d3e2a6f10'
const FIRST_KEY = '11111111-1111-4111-8111-111111111111'

const idle = (key = FIRST_KEY): ContributionState => ({
  message: null, reference: null, fieldErrors: {}, values: { memberUserId: MEMBER, amount: '' },
  idempotencyKey: key, keyBody: null, confirmation: null,
})

function form(over: Partial<Record<'memberUserId' | 'amount' | 'idempotencyKey' | 'stokvelId' | 'cycle', string>> = {}): FormData {
  const f = new FormData()
  const fields = { stokvelId: STOKVEL, memberUserId: MEMBER, cycle: '2026-11', amount: '150,50', idempotencyKey: FIRST_KEY, ...over }
  for (const [k, v] of Object.entries(fields)) f.set(k, v)
  return f
}

/** A fake API: answers come from a list, one per request; every request is written down. */
function fakePort(answers: (ApiAnswer | null)[]) {
  const sent: Parameters<RecordPort['send']>[0][] = []
  let counter = 0
  let recorded = 0
  const port: RecordPort = {
    send: async (request) => { sent.push(request); return answers[sent.length - 1] ?? null },
    newKey: () => `00000000-0000-4000-8000-${String(++counter).padStart(12, '0')}`,
    onRecorded: () => { recorded++ },
  }
  return { port, sent, recordedCount: () => recorded }
}

const ok: ApiAnswer = { status: 201, recorded: true, problem: undefined, correlationId: null, retryAfter: null }
const refusal = (status: number, problem: unknown = {}, correlationId: string | null = 'corr-1', retryAfter: string | null = null): ApiAnswer =>
  ({ status, recorded: false, problem, correlationId, retryAfter })

describe('the idempotency key', () => {
  it('a retry after a dropped connection sends the SAME key, and the next contribution sends a DIFFERENT one', async () => {
    const { port, sent } = fakePort([null, ok, ok])

    const first = await recordContributionCore(idle(), form(), port) // the connection drops
    expect(first.message).toBe(MAY_HAVE_GONE_THROUGH)
    const retry = await recordContributionCore(first, form({ idempotencyKey: first.idempotencyKey }), port) // the person presses it again
    expect(retry.confirmation).not.toBeNull()
    const next = await recordContributionCore(retry, form({ idempotencyKey: retry.idempotencyKey, amount: '99' }), port) // a new payment

    expect(sent[1].idempotencyKey).toBe(sent[0].idempotencyKey) // same on the retry
    expect(sent[2].idempotencyKey).not.toBe(sent[0].idempotencyKey) // different for the next contribution
    expect(next.confirmation?.amountCents).toBe(9900)
  })

  it('a retry after a server error (5xx) keeps the key too', async () => {
    const { port, sent } = fakePort([refusal(503), ok])
    const first = await recordContributionCore(idle(), form(), port)
    await recordContributionCore(first, form({ idempotencyKey: first.idempotencyKey }), port)
    expect(sent[1].idempotencyKey).toBe(sent[0].idempotencyKey)
  })

  it('after a success the form carries a new key and forgets the old body', async () => {
    const { port } = fakePort([ok])
    const done = await recordContributionCore(idle(), form(), port)
    expect(done.idempotencyKey).not.toBe(FIRST_KEY)
    expect(done.keyBody).toBeNull()
  })

  it('changing the amount after a failed attempt is a different request, so it gets a new key', async () => {
    const { port, sent } = fakePort([refusal(503), ok])
    const first = await recordContributionCore(idle(), form({ amount: '100' }), port)
    await recordContributionCore(first, form({ idempotencyKey: first.idempotencyKey, amount: '200' }), port)
    expect(sent[1].idempotencyKey).not.toBe(sent[0].idempotencyKey)
  })

  it('changing the member is a different request too', async () => {
    const { port, sent } = fakePort([refusal(503), ok])
    const first = await recordContributionCore(idle(), form(), port)
    await recordContributionCore(first, form({ idempotencyKey: first.idempotencyKey, memberUserId: OTHER }), port)
    expect(sent[1].idempotencyKey).not.toBe(sent[0].idempotencyKey)
  })

  it('a key that is not a real UUID is replaced, never sent', () => {
    const fresh = () => 'fresh-key'
    expect(chooseKey('not-a-uuid', null, { memberUserId: MEMBER, amountCents: 100 }, fresh)).toBe('fresh-key')
    expect(chooseKey('', null, { memberUserId: MEMBER, amountCents: 100 }, fresh)).toBe('fresh-key')
    expect(chooseKey(FIRST_KEY, null, { memberUserId: MEMBER, amountCents: 100 }, fresh)).toBe(FIRST_KEY)
  })

  it('a refusal the API gave for what was asked (409, 422) keeps the key: the API released it', async () => {
    const { port, sent } = fakePort([refusal(422), ok])
    const first = await recordContributionCore(idle(), form(), port)
    await recordContributionCore(first, form({ idempotencyKey: first.idempotencyKey }), port)
    expect(sent[1].idempotencyKey).toBe(sent[0].idempotencyKey)
  })
})

describe('what is sent', () => {
  it('sends whole cents read from the typed rands', async () => {
    const { port, sent } = fakePort([ok])
    await recordContributionCore(idle(), form({ amount: 'R 1 500,50' }), port)
    expect(sent[0]).toMatchObject({ stokvelId: STOKVEL, memberUserId: MEMBER, cycle: '2026-11', amountCents: 150_050 })
  })

  it('sends nothing for an amount that is not valid, and says why under the field', async () => {
    const { port, sent } = fakePort([ok])
    const state = await recordContributionCore(idle(), form({ amount: '10,555' }), port)
    expect(sent).toHaveLength(0)
    expect(state.fieldErrors.amount?.[0]).toMatch(/two decimals/)
    expect(state.values.amount).toBe('10,555') // what was typed is kept
  })

  it('refreshes the page once, when a contribution is recorded', async () => {
    const { port, recordedCount } = fakePort([refusal(409), ok])
    const first = await recordContributionCore(idle(), form(), port)
    expect(recordedCount()).toBe(0)
    await recordContributionCore(first, form({ idempotencyKey: first.idempotencyKey }), port)
    expect(recordedCount()).toBe(1)
  })
})

describe('what each answer means', () => {
  const run = (answer: ApiAnswer | null) => recordContributionCore(idle(), form(), fakePort([answer]).port)

  it('400: the message goes under the field, and the API field "amountCents" lands on the form field "amount"', async () => {
    const state = await run(refusal(400, { errors: { amountCents: ['Amount must be bigger than 0 cents'], cycle: ['Cycle is closed'] } }))
    expect(state.fieldErrors).toEqual({ amount: ['Amount must be bigger than 0 cents'], cycle: ['Cycle is closed'] })
    expect(state.message).toBeNull()
  })

  it('400 about a field the form does not have is shown at the top', async () => {
    const state = await run(refusal(400, { errors: { somethingNew: ['Odd.'] } }))
    expect(state.message).toBe('Odd.')
    expect(state.fieldErrors).toEqual({})
  })

  it('409 and 422: the API\'s own words at the top, with the reference', async () => {
    const s409 = await run(refusal(409, { detail: 'A contribution has already been recorded for this member in cycle 2026-11.' }, 'ref-409'))
    expect(s409.message).toBe('A contribution has already been recorded for this member in cycle 2026-11.')
    expect(s409.reference).toBe('ref-409')
    const s422 = await run(refusal(422, { detail: 'That member is not in this stokvel.' }, 'ref-422'))
    expect(s422.message).toBe('That member is not in this stokvel.')
    expect(s422.reference).toBe('ref-422')
  })

  it('403 is a refusal, not a sign-out', async () => {
    const state = await run(refusal(403))
    expect(state.message).toBe("You don't have permission to do that.")
  })

  it('429 says how long to wait', async () => {
    expect((await run(refusal(429, {}, null, '30'))).message).toBe('Try again in 30 seconds.')
  })

  it('401 is "sign in again" (the Server Action turns this into a redirect)', async () => {
    await expect(run(refusal(401))).rejects.toBeInstanceOf(SessionExpiredError)
  })
})
