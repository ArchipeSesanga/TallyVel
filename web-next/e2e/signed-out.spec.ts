import { expect, test } from '@playwright/test'

// Nobody is signed in: an empty browser (no cookies at all).
test.use({ storageState: { cookies: [], origins: [] } })

test('a signed-out visit to a signed-in page lands on sign-in, remembering where they were going', async ({ page }) => {
  await page.goto('/stokvels')

  await expect(page).toHaveURL(/\/login\?next=%2Fstokvels$/)
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
})

test('a signed-out visit to the invite page is NOT sent away: it shows a way in that remembers the invite', async ({ page }) => {
  // (The invite page is the one signed-in-looking page that answers a signed-out request, so a link preview can read it.
  // See "Metadata and indexing" in the README.)
  await page.goto('/join?code=AbC_-123&step=rules')

  await expect(page).toHaveURL(/\/join\?code=AbC_-123&step=rules$/)
  await expect(page.getByRole('heading', { level: 1, name: 'You have been invited to join a stokvel' })).toBeVisible()
  // The way in carries the address (query string and all), so signing in brings the person back to the same step and code.
  await expect(page.getByRole('link', { name: 'Sign in to continue' })).toHaveAttribute('href', '/login?next=%2Fjoin%3Fcode%3DAbC_-123%26step%3Drules')
})

test('a signed-out fetch of a /bff/ route gets a JSON 401, not an HTML page', async ({ request }) => {
  const response = await request.get('/bff/activity')

  expect(response.status()).toBe(401)
  expect(response.headers()['content-type']).toContain('application/json')
  expect(await response.json()).toMatchObject({ error: 'unauthenticated' })
})

test('a proof download link, signed out, is also a JSON 401 and never a file', async ({ request }) => {
  const response = await request.get('/bff/stokvels/4ce16b73-25dd-4a88-b36c-e8b61a24bd65/contributions/c0000002-0000-4000-8000-000000000001/proof')

  expect(response.status()).toBe(401)
  expect(response.headers()['content-type']).toContain('application/json')
})
