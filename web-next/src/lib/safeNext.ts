const DEFAULT_PATH = '/dashboard/member'

/**
 * Returns `next` only if it's a path on THIS site, otherwise the default.
 * We let the browser's own URL parser decide, so tricks like "//evil.example",
 * "/\evil.example" or hidden tabs/newlines are judged exactly as a browser would.
 */
export function safeNextPath(next: string | null | undefined): string {
  if (typeof next !== 'string' || !next.startsWith('/')) return DEFAULT_PATH

  const base = 'http://our.site'
  let resolved: URL
  try {
    resolved = new URL(next, base)
  } catch {
    return DEFAULT_PATH
  }

  // If following it would leave our site, refuse.
  if (resolved.origin !== base) return DEFAULT_PATH

  return resolved.pathname + resolved.search + resolved.hash
}