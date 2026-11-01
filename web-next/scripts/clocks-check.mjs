// Checks whether the refresh token's 7 days restart on every refresh (sliding) or are fixed at sign-in.
// Run with: DEMO_PASSWORD=... node scripts/clocks-check.mjs
const BASE = 'http://localhost:8080'
const COOKIE = 'tallyvel_rt='

/** The full Set-Cookie line for the refresh token (attributes included, so we can read its expiry). */
function refreshCookie(response) {
  return response.headers.getSetCookie().find((c) => c.startsWith(COOKIE)) ?? null
}

/** Just the token value from a Set-Cookie line. */
const token = (cookie) => cookie.split(';')[0].slice(COOKIE.length)

/** When the cookie expires, from its Expires or Max-Age attribute. */
function expires(cookie) {
  const attributes = cookie.split(';').slice(1).map((a) => a.trim())
  const expiresAttr = attributes.find((a) => a.toLowerCase().startsWith('expires='))
  if (expiresAttr) return new Date(expiresAttr.slice('expires='.length))
  const maxAge = attributes.find((a) => a.toLowerCase().startsWith('max-age='))
  if (maxAge) return new Date(Date.now() + Number(maxAge.slice('max-age='.length)) * 1000)
  throw new Error(`No Expires or Max-Age on cookie: ${cookie}`)
}

const daysFromNow = (date) => ((date.getTime() - Date.now()) / 86_400_000).toFixed(4)

// 1. Sign in: the API sets refresh token A
const login = await fetch(`${BASE}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'lerato@tallyvel.test', password: process.env.DEMO_PASSWORD }),
})
const cookieA = refreshCookie(login)
if (!cookieA) throw new Error(`Login failed (${login.status}): no refresh cookie`)
console.log('Refresh token A expires in', daysFromNow(expires(cookieA)), 'days')

// Wait a few seconds, refresh, and compare: did the 7 days start again?
await new Promise((resolve) => setTimeout(resolve, 5_000))

const refreshed = await fetch(`${BASE}/api/v1/auth/refresh`, {
  method: 'POST',
  headers: { Cookie: `${COOKIE}${token(cookieA)}` },
})
const cookieB = refreshCookie(refreshed)
if (!cookieB) throw new Error(`Refresh failed (${refreshed.status}): no refresh cookie`)
const later = expires(cookieB).getTime() - expires(cookieA).getTime()

console.log('Refresh token B expires in', daysFromNow(expires(cookieB)), 'days')
console.log(
  later > 0
    ? `B expires ${Math.round(later / 1000)} s later than A → SLIDING: each refresh restarts the clock`
    : 'B expires at the same time as A → FIXED: the clock started at sign-in',
)
