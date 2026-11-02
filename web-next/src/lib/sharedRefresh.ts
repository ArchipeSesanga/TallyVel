// The ONE place the app asks the API for new tokens (proxy.ts calls refreshSession and nothing else does).
//
// The API rotates refresh tokens and treats a reused one as theft: it revokes the whole family.
// Several requests arriving together with a nearly expired token would each call /auth/refresh with
// the SAME refresh token, and all but one would be refused, signing the person out. So concurrent
// callers share one request, and a finished result is kept for a short window under the OLD token,
// because a request that arrives just after the refresh still carries the old cookie.
//
// Plain relative imports and no Next.js imports: the test runs this file without the framework.
import type { AccessTokenResponse } from '../api/types'
import { refreshCookieHeader, refreshTokenFrom } from './refreshCookie'
import type { Session } from './session'

export type RefreshResult =
  | { kind: 'refreshed'; session: Session }
  | { kind: 'refused' }
  | { kind: 'unreachable' }

/** How long a finished result stays available under the old token. */
export const KEEP_RESULT_MS = 30_000

type Entry = { promise: Promise<RefreshResult>; finishedAt: number | null }

// Key: the refresh token that was sent to the API. It lives only in this server's memory:
// never logged, never in a cache tag or a response.
const entries = new Map<string, Entry>()

/** Forgets finished results older than the window. Runs on every call, so the map can't grow without end. */
function sweep(now: number): void {
  for (const [token, entry] of entries) {
    if (entry.finishedAt !== null && now - entry.finishedAt > KEEP_RESULT_MS) entries.delete(token)
  }
}

/** One real request to the API. Used by refreshSession only. */
async function callApi(session: Session): Promise<RefreshResult> {
  let response: Response
  try {
    response = await fetch(`${process.env.API_BASE_URL}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { Cookie: refreshCookieHeader(session.refreshToken) },
      cache: 'no-store',
    })
  } catch {
    return { kind: 'unreachable' } // the API is down: never sign anyone out for that
  }
  if (!response.ok) {
    console.log(`[refresh] the API refused the refresh with HTTP ${response.status}`)
    return { kind: 'refused' }
  }

  const body = (await response.json()) as AccessTokenResponse
  const refreshToken = refreshTokenFrom(response)
  if (refreshToken === null) return { kind: 'refused' }

  console.log(`[refresh] refreshed a session at ${new Date().toISOString()}`)
  return {
    kind: 'refreshed',
    session: { ...session, accessToken: body.accessToken, accessTokenExpiresAt: body.expiresAt, refreshToken },
  }
}

/**
 * Refreshes this session's tokens, sharing the work between callers that hold the same refresh token.
 * `now` is only a parameter so a test can move the clock.
 */
export function refreshSession(session: Session, now: () => number = Date.now): Promise<RefreshResult> {
  sweep(now())

  const existing = entries.get(session.refreshToken)
  if (existing !== undefined) return existing.promise

  const entry: Entry = { finishedAt: null, promise: undefined as unknown as Promise<RefreshResult> }
  entry.promise = callApi(session).then((result) => {
    if (result.kind === 'unreachable') {
      // No answer is not an answer to remember: the next request should try again.
      entries.delete(session.refreshToken)
    } else {
      entry.finishedAt = now()
    }
    return result
  })
  entries.set(session.refreshToken, entry)
  return entry.promise
}

/** For tests only: forget everything. */
export function resetRefreshCache(): void {
  entries.clear()
}
