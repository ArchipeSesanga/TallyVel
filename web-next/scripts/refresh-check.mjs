// Checks how the API rotates refresh tokens, the way the Next.js server will call it.
// Run with: DEMO_PASSWORD=... node scripts/refresh-check.mjs
const BASE = 'http://localhost:8080'
const COOKIE = 'tallyvel_rt='

/** Pulls the refresh token out of the Set-Cookie header (a server can read it; a browser can't). */
function refreshTokenFrom(response) {
  const cookie = response.headers.getSetCookie().find((c) => c.startsWith(COOKIE))
  return cookie ? cookie.split(';')[0].slice(COOKIE.length) : null
}

function refresh(token) {
  return fetch(`${BASE}/api/v1/auth/refresh`, {
    method: 'POST',
    headers: { Cookie: `${COOKIE}${token}` },
  })
}

const short = (t) => (t ? `${t.slice(0, 8)}…` : 'none')

// 1. Sign in: the API sets refresh token A
const login = await fetch(`${BASE}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'lerato@tallyvel.test', password: process.env.DEMO_PASSWORD }),
})
const tokenA = refreshTokenFrom(login)
console.log('1. Login            ', login.status, '| refresh token A:', short(tokenA))

// 2. Refresh with A: the API should answer with a NEW refresh token B
const first = await refresh(tokenA)
const tokenB = refreshTokenFrom(first)
const body = await first.json().catch(() => ({}))
console.log('2. Refresh with A   ', first.status, '| new token B:', short(tokenB), '| access token in body:', 'accessToken' in body)

// 3. Send A again: it's already used up, so this looks like theft
const reuse = await refresh(tokenA)
console.log('3. Reuse A (old)    ', reuse.status)

// 4. Now try B, the token that was legitimately issued in step 2
const afterReuse = await refresh(tokenB)
console.log('4. B after A reused ', afterReuse.status, afterReuse.status === 401 ? '→ B was revoked too: the whole family is gone' : '→ B still works')