import { api } from '@/lib/api'
import { getSession } from '@/lib/session'
import { badRequest, fromApiFailure, pageTokenFrom, unauthenticated, unreachable } from '@/lib/bff'

// GET /bff/activity?pageToken=...  → the next page of the member's activity feed, as JSON.
// The browser calls this (our own site); only this server calls the API, with the token from the session cookie.
export async function GET(request: Request) {
  // No session → a JSON 401. (proxy.ts already answers this for requests with no cookie at all.)
  const session = await getSession()
  if (session === null) return unauthenticated()

  const pageToken = pageTokenFrom(new URL(request.url).searchParams)
  if (pageToken === null) return badRequest('The page token is not valid.')

  const result = await api
    .GET('/api/v1/me/activity', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { query: { pageToken: pageToken === '' ? undefined : pageToken } },
    })
    .catch(() => null)

  if (result === null) return unreachable()
  if (result.data === undefined) return fromApiFailure(result.response, result.error)
  return Response.json(result.data)
}
