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

  // Split the digits as text: "15050" → rands "150", cents "50". No division, so no fractions.
  const digits = String(whole).padStart(3, '0')
  const randsPart = digits.slice(0, -2)
  const centsPart = digits.slice(-2)

  const exact = `${sign}${randsPart}.${centsPart}` // "150.50"

  return rands.format(exact as Intl.StringNumericLiteral)
}