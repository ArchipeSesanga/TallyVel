const rands = new Intl.NumberFormat('en-ZA', {
  style: 'currency',
  currency: 'ZAR',
})

/**
 * Formats an integer number of cents as Rands, e.g. 15050 → "R 150,50".
 * Never turns the cents into a fractional number.
 */
export function formatCents(cents: number): string {
  if (!Number.isSafeInteger(cents)) {
    throw new Error(`Not a whole number of cents: ${cents}`)
  }

  const sign = cents < 0 ? '-' : ''
  const whole = Math.abs(cents)

  const centsPart = whole % 100               // 15050 → 50
  const randsPart = (whole - centsPart) / 100 // (15050 - 50) / 100 → 150, always exact

  const exact = `${sign}${randsPart}.${String(centsPart).padStart(2, '0')}` // "150.50"

  return rands.format(exact as Intl.StringNumericLiteral)
}