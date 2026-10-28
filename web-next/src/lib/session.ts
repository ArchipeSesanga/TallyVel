import 'server-only'
import { cookies } from 'next/headers'
import { EncryptJWT, jwtDecrypt } from 'jose'

export type Session = {
  accessToken: string
  refreshToken: string
  user: { id: string; email: string; fullName: string }
}

const COOKIE_NAME = 'session'
const SEVEN_DAYS = 7 * 24 * 60 * 60 // seconds; the refresh token's lifetime

/** The 32-byte key from SESSION_SECRET. Fails loudly if it's missing or the wrong size. */
function secretKey(): Uint8Array {
  const secret = process.env.SESSION_SECRET
  if (!secret) throw new Error('SESSION_SECRET is not set (see .env.example)')
  const key = new Uint8Array(Buffer.from(secret, 'base64'))
  if (key.length !== 32) throw new Error('SESSION_SECRET must be 32 bytes, base64-encoded')
  return key
}

export async function createSession(session: Session): Promise<void> {
  // 'dir' + A256GCM: encrypted AND tamper-proof with one shared key.
  const value = await new EncryptJWT({ ...session })
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime('7d')
    .encrypt(secretKey())

  const cookieStore = await cookies()
  cookieStore.set(COOKIE_NAME, value, {
    httpOnly: true, // JavaScript in the browser can't read it
    secure: true, // HTTPS only (Chrome allows http://localhost)
    sameSite: 'lax', // sent when following a link to the site, not on cross-site POSTs
    path: '/',
    maxAge: SEVEN_DAYS,
  })
}

/** The session, or null. A cookie that fails to decrypt or has the wrong shape counts as no session. */
export async function getSession(): Promise<Session | null> {
  const value = (await cookies()).get(COOKIE_NAME)?.value
  if (!value) return null

  try {
    const { payload } = await jwtDecrypt(value, secretKey())
    return isSession(payload) ? payload : null
  } catch {
    return null
  }
}

export async function deleteSession(): Promise<void> {
  ;(await cookies()).delete(COOKIE_NAME)
}

function isSession(value: unknown): value is Session {
  if (typeof value !== 'object' || value === null) return false
  const v = value as Record<string, unknown>
  const user = v.user as Record<string, unknown> | undefined
  return (
    typeof v.accessToken === 'string' &&
    typeof v.refreshToken === 'string' &&
    typeof user === 'object' &&
    user !== null &&
    typeof user.id === 'string' &&
    typeof user.email === 'string' &&
    typeof user.fullName === 'string'
  )
}