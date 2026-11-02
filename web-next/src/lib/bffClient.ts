// The browser's way to ask OUR site (/bff/...) for more data. Never the API's port, never a token:
// the session cookie goes along by itself and the server adds the token.

/** A failed /bff/ request. `status` is the HTTP status, so 4xx can be told from 5xx. */
export class BffRequestError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly reference: string | null = null,
  ) {
    super(message)
  }
}

export async function fetchBffJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  let response: Response
  try {
    response = await fetch(url, { signal, headers: { Accept: 'application/json' } })
  } catch (error) {
    if (signal?.aborted) throw error // the request was cancelled on purpose (the filter changed)
    throw new BffRequestError(0, "Can't reach RondiTrack. Check your connection.")
  }

  if (response.status === 401) {
    // The session has ended: sign in again, then come back to this page. A full page load on purpose:
    // it throws away everything the old session loaded, which a client-side navigation would keep.
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination
    window.location.assign(`/login?expired=1&next=${encodeURIComponent(window.location.pathname + window.location.search)}`)
    throw new BffRequestError(401, 'Your session has ended. Please sign in again.')
  }

  if (!response.ok) {
    const body = (await response.json().catch(() => ({}))) as { message?: string; reference?: string | null }
    throw new BffRequestError(response.status, body.message ?? 'Something went wrong. Please try again.', body.reference ?? null)
  }
  return (await response.json()) as T
}
