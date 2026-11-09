import { expect, test } from '@playwright/test'
import { formatCents } from '../src/lib/money'
import { parseRandsToCents } from '../src/lib/moneyInput'
import { createStokvelWithOpenCycle, uniqueName } from './support/api'
import { PEOPLE } from './support/env'

test.use({ storageState: PEOPLE.lerato.file })

// The journey a treasurer takes most often. It makes its OWN stokvel (a unique name, an open cycle), so it does not
// depend on the seeded stokvels or on any other test, and running it twice does not collide.

test('record R 150,50 and see the cycle total rise by exactly 15050 cents, without a reload', async ({ page, request }, testInfo) => {
  const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('E2E journey', testInfo.workerIndex), 50_000)

  await page.goto('/stokvels')
  // The stokvel's card is one big link; its heading is the way a person points at it.
  await page.getByRole('heading', { level: 3, name: stokvel.name }).click()
  await expect(page.getByRole('heading', { level: 1, name: stokvel.name })).toBeVisible()

  // 1. READ the current total first (on a new stokvel it is R 0,00, but the test does not assume that).
  const total = page.getByText(/ collected of /)
  const before = parseCollectedCents(await total.innerText())

  // A marker that only survives if the page is NOT reloaded.
  await page.evaluate(() => { (window as unknown as { __notReloaded: boolean }).__notReloaded = true })

  // 2. Record R 150,50 (typed the South African way).
  await page.getByLabel('Amount (rands)').fill('150,50')
  await page.getByRole('button', { name: 'Record contribution' }).click()

  // 3. The form says what was recorded, and the total is exactly 15050 cents higher.
  await expect(page.getByRole('status').filter({ hasText: 'Recorded' })).toContainText('Recorded R 150,50 for you')
  await expect(total).toHaveText(new RegExp(`^${asPattern(formatCents(before + 15_050))}\\s+collected of `))

  // 4. ...and the page was never reloaded.
  expect(await page.evaluate(() => (window as unknown as { __notReloaded?: boolean }).__notReloaded)).toBe(true)
})

/** "R 1 500,50 collected of R 5 000,00" → 150050. The amount is read with the app's own strict parser. */
function parseCollectedCents(text: string): number {
  const amount = /^(.*?)\s+collected of /.exec(text.trim())?.[1]
  const cents = amount === undefined ? null : parseRandsToCents(amount)
  if (cents === null) throw new Error(`could not read the collected total from: ${text}`)
  return cents
}

/** Text that may contain no-break spaces → a regular expression that accepts any space in their place. */
function asPattern(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/\s/g, '\\s')
}
