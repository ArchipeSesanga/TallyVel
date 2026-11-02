import { NextResponse, type NextRequest } from 'next/server'
import type { AccessTokenResponse } from '@/api/types'
import { SESSION_COOKIE, decryptSession, encryptSession, sessionCookieOptions, type Session } from '@/lib/session' // NEW
import { refreshCookieHeader, refreshTokenFrom } from '@/lib/refreshCookie' // NEW

/** The request header that carries the page the person asked for (path + query). Set only here. */
export const NEXT_PATH_HEADER = 'x-next-path'

/** NEW: refresh when less than this is left (60 s by default; REFRESH_MARGIN_SECONDS in .env.local overrides it for testing). */
function refreshMarginMs(): number {
  return Number(process.env.REFRESH_MARGIN_SECONDS ?? '60') * 1000
}

type RefreshResult = { kind: 'refreshed'; session: Session } | { kind: 'refused' } | { kind: 'unreachable' }

/** NEW: asks the API for new tokens. This is the ONLY place in the app that refreshes. */
async function refresh(session: Session): Promise<RefreshResult> {
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
  if (!response.ok) return { kind: 'refused' }

  const body = (await response.json()) as AccessTokenResponse
  const refreshToken = refreshTokenFrom(response)
  if (refreshToken === null) return { kind: 'refused' }

  console.log(`[proxy] refreshed the session at ${new Date().toISOString()}`)
  return {
    kind: 'refreshed',
    session: { ...session, accessToken: body.accessToken, accessTokenExpiresAt: body.expiresAt, refreshToken },
  }
}

/**
 * Runs before every page and Server Action (except /login and /register, see the matcher):
 * 1. no session → /login?next=<this page>
 * 2. access token nearly used up → refresh it, written into this request AND onto the response
 * 3. passes the page's own address on to Server Components (x-next-path)
 */
export async function proxy(request: NextRequest) {
  const url = request.nextUrl.clone()
  url.searchParams.delete('_rsc') // Next's own marker on client-side navigations, not part of the page's address
  const nextPath = url.pathname + url.search

  // NEW 1. No session (missing, forged, expired or an old shape): off to sign in, remembering where they were going.
  const cookieValue = request.cookies.get(SESSION_COOKIE)?.value
  const session = cookieValue ? await decryptSession(cookieValue) : null
  if (session === null) {
    const login = new URL('/login', request.url)
    login.searchParams.set('next', nextPath)
    return NextResponse.redirect(login)
  }

  // NEW 2. Token nearly used up: refresh before the page renders.
  let refreshedValue: string | null = null
  if (Date.parse(session.accessTokenExpiresAt) - Date.now() <= refreshMarginMs()) {
    const result = await refresh(session)

    if (result.kind === 'refused') {
      const response = NextResponse.redirect(new URL('/login?expired=1', request.url))
      response.cookies.delete(SESSION_COOKIE)
      return response
    }
    if (result.kind === 'refreshed') {
      refreshedValue = await encryptSession(result.session)
      // a) Into THIS request, so the page about to render already sees the new token.
      request.cookies.set(SESSION_COOKIE, refreshedValue)
    }
    // 'unreachable': carry on with the old session; the page shows "Can't reach RondiTrack".
  }

  // Yours: pass the page's address along. Built AFTER the cookie update above, so the new
  // session travels too. `set` overwrites anything the browser sent under the same name.
  const headers = new Headers(request.headers)
  headers.set(NEXT_PATH_HEADER, nextPath)
  const response = NextResponse.next({ request: { headers } })

  // b) NEW: onto the response, so the browser keeps the new session for next time.
  if (refreshedValue !== null) response.cookies.set(SESSION_COOKIE, refreshedValue, sessionCookieOptions)

  return response
}

export const config = {
  // Pages only: not assets, and not the sign-in and register pages (they never redirect to /login).
  matcher: ['/((?!_next/static|_next/image|favicon.ico|login|register).*)'],
}