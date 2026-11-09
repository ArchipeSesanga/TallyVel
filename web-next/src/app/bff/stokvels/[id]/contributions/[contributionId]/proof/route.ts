import { getSession } from '@/lib/session'
import { fromApiFailure, unauthenticated, unreachable } from '@/lib/bff'
import { isUuid } from '@/lib/ids'

// GET /bff/stokvels/{id}/contributions/{contributionId}/proof
//
// The only way a browser gets a proof. It is never a public link: this handler checks the session, adds the
// person's token and asks the API, which decides who may see the file. We stream the API's answer straight
// back (the file is never held in memory here) with headers that stop the browser from showing, guessing or
// keeping it.

const SAFE_TYPES = new Set(['image/jpeg', 'image/png', 'application/pdf'])

// One 404 for everything "not yours" or "not there", the same words the API uses: nobody can tell which it was.
function notFoundProof(): Response {
  return Response.json({ error: 'proof-not-found', message: 'No proof was found.' }, { status: 404, headers: { 'Cache-Control': 'private, no-store', 'X-Content-Type-Options': 'nosniff' } })
}

export async function GET(_request: Request, { params }: { params: Promise<{ id: string; contributionId: string }> }) {
  const session = await getSession()
  if (session === null) return unauthenticated()

  const { id, contributionId } = await params
  if (!isUuid(id) || !isUuid(contributionId)) return notFoundProof()

  let upstream: Response
  try {
    upstream = await fetch(`${process.env.API_BASE_URL}/api/v1/stokvels/${id}/contributions/${contributionId}/proof`, {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      cache: 'no-store',
    })
  } catch {
    return unreachable()
  }

  if (upstream.status === 404) return notFoundProof()
  if (!upstream.ok || upstream.body === null) {
    const error = await upstream.json().catch(() => undefined)
    return fromApiFailure(upstream, error)
  }

  // Only a type we allow is passed on as itself; anything else is just bytes to save (never shown by the browser).
  const type = upstream.headers.get('Content-Type')?.split(';')[0].trim().toLowerCase() ?? ''
  const headers = new Headers({
    'Content-Type': SAFE_TYPES.has(type) ? type : 'application/octet-stream',
    // The API names the file; we make sure it is an attachment (saved, not opened in the page) either way.
    'Content-Disposition': upstream.headers.get('Content-Disposition')?.toLowerCase().startsWith('attachment')
      ? upstream.headers.get('Content-Disposition')!
      : 'attachment; filename="proof"',
    'X-Content-Type-Options': 'nosniff',
    'Cache-Control': 'private, no-store',
  })
  const length = upstream.headers.get('Content-Length')
  if (length !== null) headers.set('Content-Length', length)

  return new Response(upstream.body, { status: 200, headers })
}
