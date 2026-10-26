/**
 * The ONLY file that reads or writes a token.
 *
 * Why the refresh must be single-flight for OUR API: each refresh token works exactly
 * once. If two requests refresh at the same moment with the same cookie, our API sees
 * the second one as token reuse and revokes the whole session (Assignment 6.1), which
 * signs the member out. So however many requests ask, only one refresh is ever in flight.
 */
import type { AccessTokenResponse } from '../api/types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL

// The access token lives only in this variable: never in localStorage, sessionStorage,
// a cookie set from JavaScript, React state or the query cache. A page reload wipes it,
// and the HttpOnly refresh cookie (which JavaScript can't read) gets a new one.
let accessToken: string | null = null

// The refresh that is in flight right now, shared by everyone who asks. null when none is.
let refreshing: Promise<string | null> | null = null

/** An error from the API, with what the error screens need. */
export class ApiError extends Error {
  readonly status: number
  readonly body: unknown
  readonly retryAfter: string | null

  constructor(status: number, body: unknown, retryAfter: string | null) {
    super(`API request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.body = body
    this.retryAfter = retryAfter
  }
}

/** Turns a failed response into an ApiError. */
export async function toApiError(response: Response): Promise<ApiError> {
  let body: unknown = null
  try {
    body = await response.json()
  } catch {
    // The body was empty or not JSON. That's fine: the status still says what happened.
  }
  return new ApiError(response.status, body, response.headers.get('Retry-After'))
}

// ---------- "signed out" listeners ----------

const signedOutListeners = new Set<() => void>()

/** Lets the app react when the member is signed out. Returns a function to stop listening. */
export function onSignedOut(listener: () => void): () => void {
  signedOutListeners.add(listener)
  return () => {
    signedOutListeners.delete(listener)
  }
}

function notifySignedOut() {
  accessToken = null
  signedOutListeners.forEach((listener) => listener())
}

// ---------- login / logout / refresh ----------

export async function login(email: string, password: string): Promise<void> {
  const response = await fetch(`${BASE_URL}/api/v1/auth/login`, {
    method: 'POST',
    credentials: 'include', // lets the browser store the HttpOnly refresh cookie
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!response.ok) throw await toApiError(response)

  const body = (await response.json()) as AccessTokenResponse
  accessToken = body.accessToken
}

export async function logout(): Promise<void> {
  try {
    await fetch(`${BASE_URL}/api/v1/auth/logout`, { method: 'POST', credentials: 'include' })
  } catch {
    // Even if the network call fails, the member asked to sign out, so we still do.
  } finally {
    notifySignedOut()
  }
}

/**
 * Single-flight: the first caller starts the refresh; everyone who asks while it runs
 * gets the SAME promise. When it finishes (success or failure), `refreshing` goes back
 * to null, so the next expiry can start a new refresh.
 */
export function refreshAccessToken(): Promise<string | null> {
  refreshing ??= doRefresh().finally(() => {
    refreshing = null
  })
  return refreshing
}

async function doRefresh(): Promise<string | null> {
  const response = await fetch(`${BASE_URL}/api/v1/auth/refresh`, {
    method: 'POST',
    credentials: 'include', // sends the HttpOnly refresh cookie
  })
  if (!response.ok) {
    // No cookie, or it expired or was revoked: the session is over.
    // This runs once per refresh, so the app is told "signed out" once, not once per request.
    notifySignedOut()
    return null
  }
  const body = (await response.json()) as AccessTokenResponse
  accessToken = body.accessToken
  return accessToken
}

// ---------- authFetch ----------

function withToken(request: Request, token: string): Request {
  const copy = new Request(request)
  copy.headers.set('Authorization', `Bearer ${token}`)
  return copy
}

/**
 * The fetch we hand to openapi-fetch. It:
 * - refreshes first when memory is empty (after a page reload), using the cookie;
 * - adds the Authorization header;
 * - on a 401, refreshes once and retries once.
 */
export async function authFetch(request: Request): Promise<Response> {
  // A request body can only be read once. Keep a copy for the retry BEFORE sending.
  const retryCopy = request.clone()

  let token = accessToken ?? (await refreshAccessToken())

  // No token even after trying the cookie: send once and return the 401 as it is.
  // Refreshing and retrying again here would loop forever (the trap from Day 3).
  if (token === null) return fetch(request)

  const response = await fetch(withToken(request, token))
  if (response.status !== 401) return response

  // The token expired. If another request already got a fresh one while we waited,
  // use it instead of refreshing again.
  token = accessToken !== null && accessToken !== token ? accessToken : await refreshAccessToken()
  if (token === null) return response

  return fetch(withToken(retryCopy, token)) // retry ONCE, never more
} 