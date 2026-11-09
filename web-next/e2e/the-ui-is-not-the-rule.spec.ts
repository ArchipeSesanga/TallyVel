import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import { addAsMember, createStokvelWithOpenCycle, uniqueName, userIdOf } from './support/api'
import { PEOPLE } from './support/env'

// The page hides what a Member may not do, but hiding is not the rule: the API is. These tests do what a person with
// browser tools could do (change a hidden field, call a route by hand) and check the API refuses, the person is told
// so in words, and nobody is signed out.

test.use({ storageState: PEOPLE.sipho.file })

// Each test makes its own stokvel: Lerato creates it (so she is its Chairperson) with an open cycle and adds Sipho as an
// ordinary Member. Nothing depends on the state of the seeded stokvels, which people change when they use the app.
test.describe('Sipho, only a Member of a stokvel', () => {
  test('a forged "record for another member" is refused by the API, shown as a message, and he stays signed in', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('E2E member', testInfo.workerIndex), 50_000)
    await addAsMember(request, 'lerato', stokvel.id, 'sipho')
    const lerato = await userIdOf(request, 'lerato')

    await page.goto(`/stokvels/${stokvel.id}`)
    await expect(page.getByRole('heading', { level: 1, name: stokvel.name })).toBeVisible()
    // The page offers a Member nothing for other people...
    await expect(page.getByLabel('Payment for')).toHaveCount(0)

    // ...so the request is forged: the hidden "who is this payment for" field is changed to Lerato's id.
    await page.locator('form input[name="memberUserId"]').evaluate((field, id) => { (field as HTMLInputElement).value = id }, lerato)
    await page.getByLabel('Amount (rands)').fill('10')
    await page.getByRole('button', { name: 'Record contribution' }).click()

    // The API's own words for its 403 arrive as a message at the top of the form, with a reference to quote.
    const refusal = page.getByRole('alert').filter({ hasText: 'Only this stokvel' })
    await expect(refusal).toContainText("Only this stokvel's Treasurer or Chairperson can record a contribution for another member.")
    await expect(refusal).toContainText('Reference:')
    await expect(page.getByRole('status')).toHaveCount(0) // nothing was recorded

    // He is still signed in: the next page is a signed-in page, not the sign-in page.
    await page.goto('/dashboard/member')
    await expect(page).toHaveURL(/\/dashboard\/member/)
    await expect(page.getByRole('heading', { name: 'Member dashboard' })).toBeVisible()
  })

  test('calling the treasurer\'s /bff/ route by hand gets the API\'s 403 as JSON, and he is still signed in', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('E2E member', testInfo.workerIndex), 50_000)
    await addAsMember(request, 'lerato', stokvel.id, 'sipho')

    // page.request shares the page's cookies, so this is Sipho calling the route from his own browser.
    const response = await page.request.get(`/bff/stokvels/${stokvel.id}/cycles/${randomUUID()}/close-preview`)

    expect(response.status()).toBe(403)
    expect(response.headers()['content-type']).toContain('application/json')
    expect(await response.json()).toMatchObject({ error: 'api-failure', message: "You don't have permission to do that." })

    await page.goto('/dashboard/member')
    await expect(page).toHaveURL(/\/dashboard\/member/)
  })
})
