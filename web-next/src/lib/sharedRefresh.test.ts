import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { KEEP_RESULT_MS, refreshSession, resetRefreshCache } from './sharedRefresh'
import type { Session } from './session'

const sessionWith = (refreshToken: string): Session => ({
  accessToken: 'access-old',
  accessTokenExpiresAt: '2026-01-01T00:00:00Z',
  refreshToken,
  user: { id: 'u1', email: 'lerato@example.test', fullName: 'Lerato' },
})

/** What the API sends back for a good refresh: a new access token in the body, the new refresh token in Set-Cookie. */
function goodResponse(): Response {
  return new Response(JSON.stringify({ accessToken: 'access-new', expiresAt: '2026-01-01T00:15:00Z' }), {
    status: 200,
    headers: { 'Content-Type': 'application/json', 'Set-Cookie': 'tallyvel_rt=refresh-new; path=/api/v1/auth; HttpOnly' },
  })
}

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

/** Stubs fetch with a short delay, so concurrent calls really overlap. */
function stubApi(makeResponse: () => Response) {
  const fetchStub = vi.fn(async () => {
    await wait(20)
    return makeResponse()
  })
  vi.stubGlobal('fetch', fetchStub)
  return fetchStub
}

describe('refreshSession', () => {
  beforeEach(() => {
    resetRefreshCache()
    vi.spyOn(console, 'log').mockImplementation(() => {}) // no refresh log lines in the test output
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('five concurrent calls with the same token make exactly one request and share the new session', async () => {
    const fetchStub = stubApi(goodResponse)

    const results = await Promise.all(Array.from({ length: 5 }, () => refreshSession(sessionWith('refresh-A'))))

    expect(fetchStub).toHaveBeenCalledTimes(1)
    for (const result of results) {
      expect(result.kind).toBe('refreshed')
      if (result.kind === 'refreshed') {
        expect(result.session.accessToken).toBe('access-new')
        expect(result.session.refreshToken).toBe('refresh-new')
      }
    }
    expect(results.every((r) => r === results[0])).toBe(true) // the very same result object
  })

  it('a call made 1 second after the first finished, with the old token, gets the stored result without a new request', async () => {
    let clock = 1_000_000
    const fetchStub = stubApi(goodResponse)

    const first = await refreshSession(sessionWith('refresh-A'), () => clock)
    clock += 1_000
    const late = await refreshSession(sessionWith('refresh-A'), () => clock)

    expect(fetchStub).toHaveBeenCalledTimes(1)
    expect(late).toBe(first)
  })

  it('forgets a finished result after the window, so an old token is not remembered for ever', async () => {
    let clock = 1_000_000
    const fetchStub = stubApi(goodResponse)

    await refreshSession(sessionWith('refresh-A'), () => clock)
    clock += KEEP_RESULT_MS + 1
    await refreshSession(sessionWith('refresh-A'), () => clock)

    expect(fetchStub).toHaveBeenCalledTimes(2)
  })

  it('a refused refresh is shared too, and no caller retries it', async () => {
    const fetchStub = stubApi(() => new Response(null, { status: 401 }))

    const results = await Promise.all(Array.from({ length: 5 }, () => refreshSession(sessionWith('refresh-B'))))
    const late = await refreshSession(sessionWith('refresh-B'))

    expect(fetchStub).toHaveBeenCalledTimes(1)
    expect(results.every((r) => r.kind === 'refused')).toBe(true)
    expect(late.kind).toBe('refused')
  })

  it('different refresh tokens are different people: each gets its own request', async () => {
    const fetchStub = stubApi(goodResponse)

    await Promise.all([refreshSession(sessionWith('refresh-C')), refreshSession(sessionWith('refresh-D'))])

    expect(fetchStub).toHaveBeenCalledTimes(2)
  })

  it('an unreachable API is shared while in flight but not remembered, so the next request tries again', async () => {
    const fetchStub = vi.fn(async () => {
      await wait(20)
      throw new TypeError('fetch failed')
    })
    vi.stubGlobal('fetch', fetchStub)

    const results = await Promise.all(Array.from({ length: 3 }, () => refreshSession(sessionWith('refresh-E'))))
    await refreshSession(sessionWith('refresh-E'))

    expect(results.every((r) => r.kind === 'unreachable')).toBe(true)
    expect(fetchStub).toHaveBeenCalledTimes(2) // one shared by the three, one for the later retry
  })
})
