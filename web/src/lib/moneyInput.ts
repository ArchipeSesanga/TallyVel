/**
 * Reads what a member typed ("150", "150.5", "1 500,50") as an integer number of cents.
 * Returns null for anything that is not a plain, non-negative amount with at most two
 * decimals. Works on the text, never on a fractional number, so 19.99 is exactly 1999.
 */
export function parseRandsToCents(text: string): number | null {
  // South African input: spaces group thousands, a comma may be the decimal mark.
  const cleaned = text.replace(/[\s\u00a0]/g, '').replace(',', '.')

  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(cleaned)
  if (match === null) return null

  const [, randsPart, centsPart = ''] = match
  const cents = Number(randsPart + centsPart.padEnd(2, '0'))
  return Number.isSafeInteger(cents) ? cents : null
}

/** Writes cents as editable text for an input, e.g. 15050 -> "150.50", 5 -> "0.05". */
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new Error(`Not a non-negative whole number of cents: ${cents}`)
  }
  const digits = String(cents).padStart(3, '0')
  return `${digits.slice(0, -2)}.${digits.slice(-2)}`
}
