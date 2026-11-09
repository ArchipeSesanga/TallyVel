import { expect, test } from '@playwright/test'
import { PEOPLE } from './support/env'

// Proofs of payment are private. next/image's optimizer keeps ONE cache that every visitor shares, so a proof must never
// pass through it. next.config.ts allow-lists the only local file the optimizer may touch (the site's own logo); these
// tests prove a proof, or anything else under /bff/, is refused, while the logo is still served.

test.use({ storageState: PEOPLE.lerato.file })

const A_PROOF = '/bff/stokvels/4ce16b73-25dd-4a88-b36c-e8b61a24bd65/contributions/c0000002-0000-4000-8000-000000000001/proof'

test('the image optimizer refuses a proof of payment (even for a signed-in person)', async ({ request }) => {
  const response = await request.get(`/_next/image?url=${encodeURIComponent(A_PROOF)}&w=256&q=75`)
  expect(response.status()).toBe(400)
  expect(response.headers()['content-type'] ?? '').not.toMatch(/^image\//)
})

test('the image optimizer refuses any other private path too', async ({ request }) => {
  for (const path of ['/bff/activity', '/stokvels', '/api/anything']) {
    const response = await request.get(`/_next/image?url=${encodeURIComponent(path)}&w=256&q=75`)
    expect(response.status(), path).toBe(400)
  }
})

test('the site\'s own logo is still served by the optimizer (the allow-list is not a blanket ban)', async ({ request }) => {
  const response = await request.get('/_next/image?url=%2FLogoWrap.png&w=256&q=75')
  expect(response.status()).toBe(200)
  expect(response.headers()['content-type']).toMatch(/^image\//)
})

test('no page puts a proof in an <img>: a proof is only ever a download link to the /bff/ route', async ({ page }) => {
  await page.goto('/dashboard/member')
  await expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
  const sources = await page.locator('img').evaluateAll((images) => images.map((i) => (i as HTMLImageElement).src))
  expect(sources.filter((src) => src.includes('/bff/'))).toEqual([])
})
