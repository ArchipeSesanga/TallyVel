// How the API's refresh token travels. A browser hides Set-Cookie from JavaScript; a server can read it.

const REFRESH_COOKIE = 'tallyvel_rt'

/** The Cookie header that makes the API see the refresh token the way it would from a browser. */
export function refreshCookieHeader(refreshToken: string): string {
  return `${REFRESH_COOKIE}=${refreshToken}`
}

/** The refresh token the API just set on this response, or null if it set none. */
export function refreshTokenFrom(response: Response): string | null {
  const cookie = response.headers.getSetCookie().find((c) => c.startsWith(`${REFRESH_COOKIE}=`))
  return cookie ? cookie.split(';')[0].slice(REFRESH_COOKIE.length + 1) : null
}
