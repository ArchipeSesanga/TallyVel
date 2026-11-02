import 'server-only'
import { headers } from 'next/headers'
import { NEXT_PATH_HEADER } from '@/proxy'

/**
 * Where to send someone who isn't signed in: /login, with `next` set to the page they asked for
 * so signing in brings them back to it. `expired` adds the "your session ended" notice.
 * Must be called inside <Suspense> (it reads request headers).
 */
export async function loginPath(options: { expired?: boolean } = {}): Promise<string> {
  const params = new URLSearchParams()
  if (options.expired) params.set('expired', '1')

  const asked = (await headers()).get(NEXT_PATH_HEADER)
  // The home page just redirects to the dashboard, so there is nothing worth coming back to.
  if (asked !== null && asked !== '/') params.set('next', asked)

  const query = params.toString()
  return query === '' ? '/login' : `/login?${query}`
}
