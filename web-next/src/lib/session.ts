import 'server-only'
import { cookies } from 'next/headers'
import { EncryptJWT, jwtDecrypt } from 'jose'

export type Session = {
  accessToken: string
  // When the access token stops working (ISO date-time, from the API). proxy.ts refreshes shortly before.
  accessTokenExpiresAt: string
  refreshToken: string
  user: { id: string; email: string; fullName: string }
}

export const SESSION_COOKIE = 'session'
const SEVEN_DAYS = 7 * 24 * 60 * 60 // seconds; the refresh token's lifetime

export const sessionCookieOptions = {
  httpOnly: true, // JavaScript in the browser can't read it
  secure: true, // HTTPS only (Chrome allows http://localhost)
  sameSite: 'lax' as const, // sent when following a link to the site, not on cross-site POSTs
  path: '/',
  maxAge: SEVEN_DAYS,
}

/** The 32-byte key from SESSION_SECRET. Fails loudly if it's missing or the wrong size. */
function secretKey(): Uint8Array {
  const secret = process.env.SESSION_SECRET
  if (!secret) throw new Error('SESSION_SECRET is not set (see .env.example)')
  const key = new Uint8Array(Buffer.from(secret, 'base64'))
  if (key.length !== 32) throw new Error('SESSION_SECRET must be 32 bytes, base64-encoded')
  return key
}

/** The cookie value for a session. 'dir' + A256GCM: encrypted AND tamper-proof with one shared key. */
export async function encryptSession(session: Session): Promise<string> {
  return new EncryptJWT({ ...session })
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime('7d')
    .encrypt(secretKey())
}

/** The session in a cookie value, or null. A value that fails to decrypt or has the wrong shape counts as no session. */
export async function decryptSession(value: string): Promise<Session | null> {
  try {
    const { payload } = await jwtDecrypt(value, secretKey())
    return isSession(payload) ? payload : null
  } catch {
    return null
  }
}

export async function createSession(session: Session): Promise<void> {
  const value = await encryptSession(session)
  ;(await cookies()).set(SESSION_COOKIE, value, sessionCookieOptions)
}

/** The session, or null. */
export async function getSession(): Promise<Session | null> {
  const value = (await cookies()).get(SESSION_COOKIE)?.value
  return value ? decryptSession(value) : null
}

export async function deleteSession(): Promise<void> {
  ;(await cookies()).delete(SESSION_COOKIE)
}

function isSession(value: unknown): value is Session {
  if (typeof value !== 'object' || value === null) return false
  const v = value as Record<string, unknown>
  const user = v.user as Record<string, unknown> | undefined
  return (
    typeof v.accessToken === 'string' &&
    typeof v.accessTokenExpiresAt === 'string' &&
    typeof v.refreshToken === 'string' &&
    typeof user === 'object' &&
    user !== null &&
    typeof user.id === 'string' &&
    typeof user.email === 'string' &&
    typeof user.fullName === 'string'
  )
}