import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  reactCompiler: true,
  // Pages render instantly from a static shell; anything that reads cookies or the API
  // must sit inside <Suspense> (or "use cache"), and the build fails if it doesn't.
  cacheComponents: true,
  // Links prefetch the static shell of the next page, so clicks show it immediately.
  partialPrefetching: true,
  agentRules: false,
}

export default nextConfig