// Accepts "150", "150.5", "150.50", "150,50" and "1 500,50" (spaces are ignored).
// Refuses anything that would need rounding ("150.505"), negatives, and letters.
// The rands and cents are captured as TEXT and joined, so no floating-point number
// ever exists: "150" + "50" → "15050" → 15050.
const AMOUNT = /^(\d+)(?:[.,](\d{1,2}))?$/

/** What the person typed → integer cents, or null if it isn't a valid amount. */
export function parseRandsToCents(text: string): number | null {
  const cleaned = text.replace(/\s/g, '') // removes normal and no-break spaces
  const match = AMOUNT.exec(cleaned)
  if (match === null) return null

  const rands = match[1]
  const cents = (match[2] ?? '').padEnd(2, '0') // "5" → "50", "" → "00"
  const value = Number(rands + cents)

  return Number.isSafeInteger(value) ? value : null
}

/** Integer cents → text the person can edit, e.g. 15050 → "150.50". */
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new Error(`Not a whole, non-negative number of cents: ${cents}`)
  }
  const centsPart = cents % 100
  const randsPart = (cents - centsPart) / 100 // always exact: we subtracted the remainder first
  return `${randsPart}.${String(centsPart).padStart(2, '0')}`
}