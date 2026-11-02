import { NextResponse, type NextRequest } from 'next/server'

/** The request header that carries the page the person asked for (path + query). Set only here. */
export const NEXT_PATH_HEADER = 'x-next-path'

/**
 * Server Components can't see their own URL, so this passes it along. `set` overwrites anything
 * the browser sent under the same name. The value is only trusted after safeNextPath checks it.
 */
export function proxy(request: NextRequest) {
  const url = request.nextUrl.clone()
  url.searchParams.delete('_rsc') // Next's own marker on client-side navigations, not part of the page's address

  const headers = new Headers(request.headers)
  headers.set(NEXT_PATH_HEADER, url.pathname + url.search)
  return NextResponse.next({ request: { headers } })
}

export const config = {
  // Pages only: not assets, and not the sign-in and register pages (they never redirect to /login).
  matcher: ['/((?!_next/static|_next/image|favicon.ico|login|register).*)'],
}
