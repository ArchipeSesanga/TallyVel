import { expect, test } from '@playwright/test'
import { createInviteCode, createStokvelWithOpenCycle, uniqueName } from './support/api'
import { demoPassword, PEOPLE } from './support/env'

// WCAG 2.2: 3.3.7 Redundant entry, 3.3.8 Accessible authentication, and "heard, not just seen" (4.1.3 Status messages,
// 3.3.1 Error identification). Each test drives the real app with the keyboard and clipboard like a person would.

// The clipboard belongs to the whole computer, not to one browser tab. Two workers pasting at once would paste each
// other's text, so every test in THIS file runs one after the other in a single worker. (No other file uses the clipboard.)
test.describe.configure({ mode: 'serial' })

test.describe('3.3.8 Accessible authentication (sign-in)', () => {
  test.use({ storageState: { cookies: [], origins: [] }, permissions: ['clipboard-read', 'clipboard-write'] })

  test('the fields tell a password manager what they are, and there is no puzzle or code to solve', async ({ page }) => {
    await page.goto('/login')
    await expect(page.getByLabel('Email')).toHaveAttribute('autocomplete', 'username')
    await expect(page.getByLabel('Password')).toHaveAttribute('autocomplete', 'current-password')
    // Only the two fields and the button: nothing to transcribe, no picture, no second device.
    await expect(page.locator('form input:not([type=hidden])')).toHaveCount(2)
  })

  test('both fields accept a paste (copy and paste, as a password manager or a person does), and it signs in', async ({ page }) => {
    await page.goto('/login')

    // A real clipboard paste into each field. If the page blocked paste, the fields would stay empty.
    await page.evaluate(async (text) => navigator.clipboard.writeText(text), PEOPLE.sipho.email)
    await page.getByLabel('Email').focus()
    await page.keyboard.press('ControlOrMeta+V')
    await expect(page.getByLabel('Email')).toHaveValue(PEOPLE.sipho.email)

    await page.evaluate(async (text) => navigator.clipboard.writeText(text), demoPassword())
    await page.getByLabel('Password').focus()
    await page.keyboard.press('ControlOrMeta+V')
    await expect(page.getByLabel('Password')).toHaveValue(demoPassword())

    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(/\/dashboard\/member/)
  })

  test('the page does not cancel a paste event (nothing calls preventDefault on it)', async ({ page }) => {
    await page.goto('/login')
    const blocked = await page.evaluate(() => {
      const results: boolean[] = []
      for (const id of ['email', 'password']) {
        const data = new DataTransfer()
        data.setData('text/plain', 'pasted')
        const event = new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true })
        document.getElementById(id)!.dispatchEvent(event)
        results.push(event.defaultPrevented)
      }
      return results
    })
    expect(blocked).toEqual([false, false])
  })
})

test.describe('the join flow', () => {
  test.use({ storageState: PEOPLE.sipho.file, permissions: ['clipboard-read', 'clipboard-write'] })

  test('3.3.7 Redundant entry: Back from the rules shows step 1 with the code still in the box', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('A11y join', testInfo.workerIndex), 30_000)
    const code = await createInviteCode(request, 'lerato', stokvel.id)

    await page.goto('/join')
    await page.getByLabel('Invite code').fill(code)
    await page.getByRole('button', { name: 'Continue' }).click()
    // 2. rules: its heading took focus (so a screen reader says where the person is), and says which step it is.
    const step2 = page.getByRole('heading', { level: 1, name: /Step 2 of 3/ })
    await expect(step2).toBeFocused()

    // Back: step 1 again, and the code is there. Nobody types it twice.
    await page.getByRole('link', { name: 'Back', exact: true }).click()
    await expect(page.getByRole('heading', { level: 1, name: /Step 1 of 3/ })).toBeFocused()
    await expect(page.getByLabel('Invite code')).toHaveValue(code)

    // And the browser's own Back button, from step 2, does the same.
    await page.getByRole('button', { name: 'Continue' }).click()
    await expect(step2).toBeFocused()
    await page.goBack()
    await expect(page.getByLabel('Invite code')).toHaveValue(code)

    // Step 3 shows the stokvel again from what step 2 already learned, and offers Back to the rules without asking again.
    await page.getByRole('button', { name: 'Continue' }).click()
    await page.getByRole('link', { name: 'Continue' }).click()
    await expect(page.getByRole('heading', { level: 1, name: /Step 3 of 3/ })).toBeFocused()
    await expect(page.getByText(stokvel.name, { exact: true })).toBeVisible()
  })

  test('3.3.8: the invite code can be pasted, even as a whole link', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('A11y paste', testInfo.workerIndex), 30_000)
    const code = await createInviteCode(request, 'lerato', stokvel.id)

    await page.goto('/join')
    // The step's heading takes focus when the page loads (by design: it is what a screen reader announces). Wait for
    // that to finish, then move to the box the way a person would.
    await expect(page.getByRole('heading', { level: 1, name: /Step 1 of 3/ })).toBeFocused()
    await page.evaluate(async (text) => navigator.clipboard.writeText(text), `https://app.example/join?code=${code}`)
    await page.getByLabel('Invite code').click()
    await page.keyboard.press('ControlOrMeta+V')
    await expect(page.getByLabel('Invite code')).toHaveValue(code) // the link became just its code
  })
})

test.describe('heard, not just seen', () => {
  test.use({ storageState: PEOPLE.lerato.file })

  test('a refused contribution keeps what was typed, and the message is tied to the form and announced', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('A11y refused', testInfo.workerIndex), 50_000)
    await page.goto(`/stokvels/${stokvel.id}`)
    const amount = page.getByLabel('Amount (rands)')

    await amount.fill('100')
    await page.getByRole('button', { name: 'Record contribution' }).click()
    await expect(page.getByRole('status').filter({ hasText: 'Recorded' })).toBeVisible()

    // The same person again, same cycle: the API refuses (one contribution each per cycle).
    await amount.fill('75,50')
    await page.getByRole('button', { name: 'Record contribution' }).click()
    const alert = page.getByRole('alert').filter({ hasText: 'Reference:' }) // role="alert": read out the moment it appears
    await expect(alert).toBeVisible()
    await expect(amount).toHaveValue('75,50') // 3.3.7: nothing to type again
  })

  test('a field error is announced (aria-live) and tied to the field (aria-describedby, aria-invalid)', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('A11y field', testInfo.workerIndex), 50_000)
    await page.goto(`/stokvels/${stokvel.id}`)
    const amount = page.getByLabel('Amount (rands)')
    await expect(amount).not.toHaveAttribute('aria-invalid', 'true')

    await amount.fill('10,555')
    await page.getByRole('button', { name: 'Record contribution' }).click()

    await expect(amount).toHaveAttribute('aria-invalid', 'true')
    const describedBy = await amount.getAttribute('aria-describedby')
    const message = page.locator(`[id="${describedBy}"]`)
    await expect(message).toContainText(/no more than two decimals/)
    await expect(message).toHaveAttribute('aria-live', 'polite') // an always-present live region: text arriving in it is read out
  })

  test('a toast is announced: it sits in a live region with role status', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('A11y toast', testInfo.workerIndex), 50_000)
    await page.goto(`/stokvels/${stokvel.id}?created=1`) // the redirect after creating a stokvel carries this marker

    const toast = page.locator('[data-sonner-toast]').filter({ hasText: `Created ${stokvel.name}` })
    await expect(toast).toBeVisible()
    const live = await toast.evaluate((el) => {
      const region = el.closest('[aria-live]')
      return { role: el.getAttribute('role'), ariaLive: region?.getAttribute('aria-live') ?? el.getAttribute('aria-live') }
    })
    // Either the toast itself or the notifications region around it is a polite/assertive live region.
    expect(live.ariaLive === 'polite' || live.ariaLive === 'assertive' || live.role === 'status' || live.role === 'alert').toBe(true)
  })
})
