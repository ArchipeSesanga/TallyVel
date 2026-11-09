import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  reactCompiler: true,
  // Pages render instantly from a static shell; anything that reads cookies or the API
  // must sit inside <Suspense> (or "use cache"), and the build fails if it doesn't.
  cacheComponents: true,
  // Links prefetch the static shell of the next page, so clicks show it immediately.
  partialPrefetching: true,
  agentRules: false,
  // Everything under /bff/ is private data for a signed-in person (activity, contributions, proofs of payment). These
  // are not pages, so there is no <meta> to put noindex in: the header says it. (robots.txt must NOT block them: a
  // crawler that may not fetch a URL never sees this header.)
  async headers() {
    return [{ source: '/bff/:path*', headers: [{ key: 'X-Robots-Tag', value: 'noindex, nofollow' }] }]
  },
  experimental: {
    serverActions: {
      // Proof uploads travel inside a Server Action. The API's limit is 5 MB (5 * 1024 * 1024 bytes) of FILE;
      // the request is a little bigger than the file (multipart wrapping, the form's other fields). 6 MB is
      // above 5 MB plus that overhead, so Next never refuses a file the API would accept, and a file the API
      // refuses (too big, wrong type) is refused BY THE API, which is the one that decides.
      // (The proxy also buffers request bodies, up to 10 MB by default: also above 6 MB.)
      bodySizeLimit: '6mb',
    },
  },
}

export default nextConfig