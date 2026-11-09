// Typed rands → integer cents, and back. No floating-point number is ever made: the digits are read as TEXT,
// joined as text, checked as a BigInt, and only then turned into the (whole) number of cents.
//
// ACCEPTED: 1500 · "1 500" · "1 500,50" · "R1500" · "R 1 500,50" · a no-break space between thousands ·
//           one or two decimals after "," or "." ("1500,5" is 1500 rands 50 cents) · ",5" (50 cents).
// REFUSED (returns null, never rounds): three or more decimals · "1,500.50" (is the comma a thousands mark or a
//           decimal point? we don't guess) · letters · empty · negative · more than the API's maximum.

/** The largest amount the API accepts, in cents: Money.MaxCents in the API, which is 2^53 − 1 (the largest whole number JavaScript holds exactly). */
export const MAX_CONTRIBUTION_CENTS = 9_007_199_254_740_991
const MAX_CENTS = BigInt('9007199254740991') // BigInt() rather than a 123n literal: tsconfig targets ES2017

// optional "R" · rands: plain digits, or groups of three joined by a space / no-break space / narrow no-break space ·
// optional "," or "." and one or two decimals. At least one digit overall (checked below).
const AMOUNT = /^(?:R[  ]?)?(\d{1,3}(?:[   ]\d{3})+|\d+)?(?:[.,](\d{1,2}))?$/i

/** What the person typed → integer cents, or null if it isn't a valid amount (it never rounds). */
export function parseRandsToCents(text: string): number | null {
  const match = AMOUNT.exec(text.trim())
  if (match === null) return null

  const rands = match[1]?.replace(/[   ]/g, '')
  const decimals = match[2]
  if (rands === undefined && decimals === undefined) return null // "", "R" or "R " have no digits at all

  const cents = (decimals ?? '').padEnd(2, '0') // "5" → "50", "" → "00"
  const total = BigInt(rands ?? '0') * BigInt(100) + BigInt(cents)
  if (total > MAX_CENTS) return null

  return Number(total) // whole and no bigger than 2^53 − 1, so exact
}

/** Integer cents → text the person can edit, e.g. 15050 → "150.50". Digits are split as text: no division. */
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new Error(`Not a whole, non-negative number of cents: ${cents}`)
  }
  const digits = String(cents).padStart(3, '0') // 5 → "005"
  return `${digits.slice(0, -2)}.${digits.slice(-2)}`
}
