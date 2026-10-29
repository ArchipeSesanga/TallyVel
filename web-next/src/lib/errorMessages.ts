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
