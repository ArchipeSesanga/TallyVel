import type { MetadataRoute } from 'next'

// Nothing is blocked here, on purpose. The private pages (everything signed-in, the invite page, the /bff/ downloads)
// keep themselves out of search results with noindex (a <meta> tag or an X-Robots-Tag header). A crawler can only read a
// noindex on a page it is ALLOWED to fetch: if robots.txt said "Disallow", the crawler would never see the noindex and
// could still list the address. (A signed-out crawler gets sent to the sign-in page for the private ones anyway.)
export default function robots(): MetadataRoute.Robots {
  return { rules: { userAgent: '*', allow: '/' } }
}
