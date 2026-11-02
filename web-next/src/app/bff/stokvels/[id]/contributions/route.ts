import { api } from '@/lib/api'
import { getSession } from '@/lib/session'
import { badRequest, fromApiFailure, pageTokenFrom, unauthenticated, unreachable } from '@/lib/bff'
import { loadHistoryParams } from '@/lib/historyParams'
import { apiQueryFor } from '@/lib/historyQuery'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// GET /bff/stokvels/{id}/contributions?member=&from=&to=&sort=&pageToken=  → a later page of the history, as JSON.
// It reads the filter with the SAME parsers as the page, so a filter means the same thing in both places.
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const session = await getSession()
  if (session === null) return unauthenticated()

  const { id } = await params
  if (!UUID.test(id)) return Response.json({ error: 'not-found', message: 'Stokvel not found.' }, { status: 404 })

  const { searchParams } = new URL(request.url)
  const pageToken = pageTokenFrom(searchParams)
  if (pageToken === null) return badRequest('The page token is not valid.')

  // Nonsense filter values fall back to their defaults here too (nothing is rejected).
  const filter = loadHistoryParams(searchParams)
  const { query } = apiQueryFor(filter)

  const result = await api
    .GET('/api/v1/stokvels/{stokvelId}/contributions', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: { stokvelId: id }, query: { ...query, ...(pageToken !== '' ? { pageToken } : {}) } },
    })
    .catch(() => null)

  if (result === null) return unreachable()
  if (result.data === undefined) return fromApiFailure(result.response, result.error)
  return Response.json(result.data)
}
