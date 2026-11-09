import type { Metadata } from 'next'

// One place for what the site says about itself. Nothing in here comes from a stokvel, a person or a request.

export const SITE_NAME = 'RondiTrack'
export const SITE_TITLE = 'RondiTrack: run your stokvel with trust'
export const SITE_DESCRIPTION =
  'Log every contribution, see who has paid and keep the payout schedule fair. Built for stokvels, burial societies and savings groups.'

/** The picture a link preview shows. Made by app/opengraph-image.tsx. It is the same for every page and every invite. */
export const OG_IMAGE = { url: '/opengraph-image', width: 1200, height: 630, alt: 'RondiTrack: run your stokvel with trust' }

/**
 * Where the site lives, so og:image and the other preview links are absolute (a preview bot needs the full address).
 * Set SITE_URL in production; the fallback is only for local development.
 */
export function siteUrl(): URL {
  return new URL(process.env.SITE_URL ?? 'http://localhost:3000')
}

/** The head of the invite page. See generateMetadata in join/page.tsx for why this takes no arguments. */
export function inviteMetadata(): Metadata {
  const title = `You have been invited to a stokvel on ${SITE_NAME}`
  const description = `Sign in to ${SITE_NAME} to see the invitation and join.`
  return {
    title: { absolute: title }, // already names the product; the site's "· RondiTrack" suffix would say it twice
    description,
    robots: { index: false, follow: false },
    openGraph: { type: 'website', siteName: SITE_NAME, title, description, images: [OG_IMAGE] },
    twitter: { card: 'summary_large_image', title, description, images: [OG_IMAGE.url] },
  }
}
