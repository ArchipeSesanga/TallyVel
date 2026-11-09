import { expect, test } from '@playwright/test'

// Nobody is signed in: an empty browser (no cookies at all).
test.use({ storageState: { cookies: [], origins: [] } })

test('a signed-out visit to a signed-in page lands on sign-in, remembering where they were going', async ({ page }) => {
  await page.goto('/stokvels')

  await expect(page).toHaveURL(/\/login\?next=%2Fstokvels$/)
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
})

test('the address they were going to keeps its query string (a join link comes back to the same step and code)', async ({ page }) => {
  await page.goto('/join?code=AbC_-123&step=rules')

  await expect(page).toHaveURL(/\/login\?next=%2Fjoin%3Fcode%3DAbC_-123%26step%3Drules$/)
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
