import { expect, test } from '@playwright/test'
import { createStokvelWithOpenCycle, uniqueName } from './support/api'
import { expectNoSeriousViolations } from './support/axe'
import { PEOPLE } from './support/env'

// WCAG 2.2 AA, checked by axe-core on the four screens the brief names (plus the sign-in page, which everyone sees
// first). An automated check finds only part of the problems (roughly a third); the rest are in the keyboard tests
// (keyboard.spec.ts) and in the README's "Accessibility" table, which says which was found how.

test.describe('signed out', () => {
  test.use({ storageState: { cookies: [], origins: [] } })

  test('the sign-in page', async ({ page }, testInfo) => {
    await page.goto('/login')
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
    await expectNoSeriousViolations(page, testInfo)
  })

  test('the invite page as a signed-out visitor sees it', async ({ page }, testInfo) => {
    await page.goto('/join?code=AbC_-123')
    await expect(page.getByRole('heading', { level: 1, name: /invited/ })).toBeVisible()
    await expectNoSeriousViolations(page, testInfo)
  })
})

test.describe('signed in', () => {
  test.use({ storageState: PEOPLE.lerato.file })

  test('the home screen', async ({ page }, testInfo) => {
    await page.goto('/dashboard/member')
    await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible()
    await expectNoSeriousViolations(page, testInfo)
  })

  test('a stokvel page', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('A11y stokvel', testInfo.workerIndex), 50_000)
    await page.goto(`/stokvels/${stokvel.id}`)
    await expect(page.getByRole('heading', { level: 1, name: stokvel.name })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Record contribution' })).toBeVisible()
    await expectNoSeriousViolations(page, testInfo)
  })

  test('the contribution form while it shows its errors', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('A11y errors', testInfo.workerIndex), 50_000)
    await page.goto(`/stokvels/${stokvel.id}`)

    // An amount the form's own rules refuse: the message appears under the field.
    await page.getByLabel('Amount (rands)').fill('10,555')
    await page.getByRole('button', { name: 'Record contribution' }).click()
    await expect(page.getByLabel('Amount (rands)')).toHaveAttribute('aria-invalid', 'true')
    await expect(page.getByLabel('Amount (rands)')).toHaveAccessibleDescription(/no more than two decimals/)

    await expectNoSeriousViolations(page, testInfo)
  })

  test('the open Close Cycle dialog', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('A11y close', testInfo.workerIndex), 50_000)
    await page.goto(`/stokvels/${stokvel.id}`)
    await page.getByRole('button', { name: /^Close \d{4}-\d{2}/ }).click()

    const dialog = page.getByRole('alertdialog')
    await expect(dialog).toBeVisible()
    // Wait for the API's preview to arrive, so the check sees the real content and not only the loading state.
    await expect(dialog.getByRole('button', { name: /^Close \d{4}-\d{2}.+/ }).or(dialog.getByRole('button', { name: /^Close \d{4}-\d{2}$/ }))).toBeEnabled()

    await expectNoSeriousViolations(page, testInfo)
  })
})
