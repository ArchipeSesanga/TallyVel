import { api } from '@/lib/api'
import { getSession } from '@/lib/session'
import { fromApiFailure, unauthenticated, unreachable } from '@/lib/bff'
import { isUuid } from '@/lib/ids'

// GET /bff/stokvels/{id}/cycles/{cycleId}/close-preview
// What closing the cycle WOULD do right now, worked out by the API (the same code the real close uses).
// The browser never calculates arrears; it only shows this. Never cached: a preview is only true for the moment it is made.
export async function GET(_request: Request, { params }: { params: Promise<{ id: string; cycleId: string }> }) {
  const session = await getSession()
  if (session === null) return unauthenticated()

  const { id, cycleId } = await params
  if (!isUuid(id) || !isUuid(cycleId)) return Response.json({ error: 'not-found', message: 'Cycle not found.' }, { status: 404 })

  const result = await api
    .GET('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/close-preview', {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      params: { path: { stokvelId: id, cycleId } },
    })
    .catch(() => null)

  if (result === null) return unreachable()
  if (result.data === undefined) return fromApiFailure(result.response, result.error)
  return Response.json(result.data, { headers: { 'Cache-Control': 'private, no-store' } })
}
