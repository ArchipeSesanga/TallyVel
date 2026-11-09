import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import createClient from 'openapi-fetch'
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest'
import type { paths } from '@/api/schema'
import { MAY_HAVE_GONE_THROUGH, recordContributionCore, type ContributionState, type RecordPort } from './recordContribution'
import { sendWithClient } from './recordContributionApi'

// The real request code, talking over real HTTP to a FAKE API that MSW stands in for. This is where "what the API
// really sees" is checked: the token, the Idempotency-Key header and the body, and what each kind of answer looks like.

const API = 'http://api.test'
const STOKVEL = '4ce16b73-25dd-4a88-b36c-e8b61a24bd65'
const MEMBER = 'c67160fb-d6f0-40de-96e7-261823fef0f9'
const KEY = '11111111-1111-4111-8111-111111111111'
const URL = `${API}/api/v1/stokvels/${STOKVEL}/contributions`

const server = setupServer()
// A request nobody planned for FAILS the test instead of quietly going to the real network.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

// `fetch` is looked up at call time: openapi-fetch would otherwise keep the fetch that existed before MSW started.
const client = createClient<paths>({ baseUrl: API, fetch: (request) => globalThis.fetch(request) })
type Seen = { auth: string | null; key: string | null; body: unknown }

/** Answers the requests in order and writes down what the "API" received. */
function planApi(...answers: (() => Response)[]): Seen[] {
  const seen: Seen[] = []
  let n = 0
  server.use(
    http.post(URL, async ({ request }) => {
      seen.push({ auth: request.headers.get('authorization'), key: request.headers.get('idempotency-key'), body: await request.json() })
      return answers[n++]()
    }),
  )
  return seen
}

const created = () => HttpResponse.json({ id: 'c1', stokvelId: STOKVEL, memberUserId: MEMBER, cycle: '2026-11', amountCents: 15050, status: 'Paid', recordedAt: '2026-11-09T08:00:00Z' }, { status: 201 })
const problem = (status: number, body: object, headers: Record<string, string> = {}) =>
  () => HttpResponse.json(body, { status, headers: { 'content-type': 'application/problem+json', ...headers } })

const request = { stokvelId: STOKVEL, memberUserId: MEMBER, cycle: '2026-11', amountCents: 15050, idempotencyKey: KEY }

describe('what the API receives', () => {
  it('the person\'s token, the Idempotency-Key header and the amount in whole cents', async () => {
    const seen = planApi(created)
    const answer = await sendWithClient(client, 'token-abc')(request)

    expect(answer).toMatchObject({ status: 201, recorded: true })
    expect(seen).toEqual([{ auth: 'Bearer token-abc', key: KEY, body: { memberUserId: MEMBER, cycle: '2026-11', amountCents: 15050 } }])
  })

  it('a problem+json answer keeps its status, body, correlationId and Retry-After', async () => {
    planApi(problem(429, { title: 'Too many requests', correlationId: 'corr-9' }, { 'Retry-After': '42' }))
    const answer = await sendWithClient(client, 't')(request)
    expect(answer).toMatchObject({ status: 429, recorded: false, correlationId: 'corr-9', retryAfter: '42' })
  })

  it('a dropped connection is "no answer" (null), not an exception', async () => {
    planApi(() => HttpResponse.error())
    expect(await sendWithClient(client, 't')(request)).toBeNull()
  })

  it('a request nobody planned for fails, instead of reaching a real server', async () => {
    await expect(fetch(`${API}/api/v1/nothing-planned`)).rejects.toThrow()
  })
})

describe('the retry, seen from the API\'s side', () => {
  const idle: ContributionState = {
    message: null, reference: null, fieldErrors: {}, values: { memberUserId: MEMBER, amount: '' },
    idempotencyKey: KEY, keyBody: null, confirmation: null,
  }
  const formFor = (key: string, amount: string) => {
    const f = new FormData()
    f.set('stokvelId', STOKVEL); f.set('memberUserId', MEMBER); f.set('cycle', '2026-11'); f.set('amount', amount); f.set('idempotencyKey', key)
    return f
  }
  let counter = 0
  const port = (): RecordPort => ({
    send: sendWithClient(client, 'token'),
    newKey: () => `99999999-9999-4999-8999-${String(++counter).padStart(12, '0')}`,
    onRecorded: () => {},
  })

  it('a dropped connection, a retry and then the next payment: the API sees the same key twice, then a different one', async () => {
    const seen = planApi(() => HttpResponse.error(), created, created)

    const dropped = await recordContributionCore(idle, formFor(KEY, '150,50'), port())
    expect(dropped.message).toBe(MAY_HAVE_GONE_THROUGH)
    const retried = await recordContributionCore(dropped, formFor(dropped.idempotencyKey, '150,50'), port())
    expect(retried.confirmation?.amountCents).toBe(15050)
    await recordContributionCore(retried, formFor(retried.idempotencyKey, '75'), port())

    expect(seen.map((s) => s.key)).toHaveLength(3)
    expect(seen[1].key).toBe(seen[0].key)
    expect(seen[2].key).not.toBe(seen[0].key)
    expect(seen[2].body).toMatchObject({ amountCents: 7500 })
  })
})
