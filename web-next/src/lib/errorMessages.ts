// The text for 403, 429, 500 and network errors.

/** A plain-language message for a failed API call. `retryAfter` is the Retry-After header, if any. */
export function describeApiError(status: number, retryAfter: string | null): string {
  if (status === 403) return "You don't have permission to do that."
  if (status === 429) {
    const seconds = Number(retryAfter)
    return Number.isFinite(seconds) && seconds > 0
      ? `Too many requests. Please try again in ${Math.ceil(seconds)} seconds.`
      : 'Too many requests. Please try again in a moment.'
  }
  if (status >= 500) return 'Something went wrong on our side. Please try again.'
  return 'Something went wrong. Please try again.'
}

/** "Try again in 12 seconds." from a Retry-After header (whole seconds). Falls back to "in a moment". */
export function tryAgainIn(retryAfter: string | null): string {
  const seconds = Math.ceil(Number(retryAfter))
  return retryAfter !== null && Number.isFinite(seconds) && seconds > 0 ? `Try again in ${seconds} seconds.` : 'Try again in a moment.'
}

/**
 * A reference for a failure that never reached the API (so it has no correlationId from there). It is written
 * to the server log together with the error, so "Reference: web-1a2b3c4d" can be found there.
 */
export function localReference(): string {
  return `web-${crypto.randomUUID().slice(0, 8)}`
}
