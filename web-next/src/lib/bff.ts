import 'server-only'
import { correlationIdOf } from '@/lib/api'
import { describeApiError } from '@/lib/errorMessages'

// Helpers for the Route Handlers under /bff/ ("backend for frontend"): the browser asks OUR site, our server
// asks the API with the person's token, and only the JSON comes back. The browser never talks to the API's
// port and never sees a token.

/** The JSON body of every failure from a /bff/ handler. `reference` is the API's correlation id, if there is one. */
export type BffError = { error: string; message: string; code?: string; reference?: string | null }

export function unauthenticated(): Response {
  // JSON, never an HTML redirect: the caller is a fetch() in the browser, not a person looking at a page.
  return Response.json(
    { error: 'unauthenticated', message: 'Your session has ended. Please sign in again.' } satisfies BffError,
    { status: 401 },
  )
}

export function badRequest(message: string): Response {
  return Response.json({ error: 'bad-request', message } satisfies BffError, { status: 400 })
}

/** The API refused or failed: pass its status on with a message fit for people. */
export function fromApiFailure(response: Response, error: unknown): Response {
  if (response.status === 401) return unauthenticated()

  const code = typeof (error as { code?: unknown })?.code === 'string' ? (error as { code: string }).code : undefined
  const detail = typeof (error as { detail?: unknown })?.detail === 'string' ? (error as { detail: string }).detail : null
  const retryAfter = response.headers.get('Retry-After')

  // A 4xx the API explained (a bad filter, no access) is passed on as it is; anything else is "something went wrong".
  const status = response.status >= 400 && response.status < 500 ? response.status : 502
  const message = status === 502 || status === 429 ? describeApiError(response.status, retryAfter) : (detail ?? describeApiError(response.status, retryAfter))

  return Response.json(
    { error: 'api-failure', message, code, reference: correlationIdOf(error) } satisfies BffError,
    { status, headers: retryAfter !== null ? { 'Retry-After': retryAfter } : undefined },
  )
}

/** The API could not be reached at all. */
export function unreachable(): Response {
  return Response.json(
    { error: 'unreachable', message: "Can't reach RondiTrack. Check your connection." } satisfies BffError,
    { status: 502 },
  )
}

/** A page token is opaque: we only make sure it is a plain, short string before passing it on. */
export function pageTokenFrom(searchParams: URLSearchParams): string | null {
  const token = searchParams.get('pageToken') ?? ''
  return token.length <= 2048 ? token : null
}
