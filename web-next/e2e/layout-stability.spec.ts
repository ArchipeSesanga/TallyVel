import { expect, test, type Page } from '@playwright/test'
import { stokvelIdByName } from './support/api'
import { PEOPLE } from './support/env'

// Skeletons are a CLS test: a placeholder that is shorter or taller than the content that replaces it makes the page
// jump. This measures the real thing in a real browser, with the browser's own Layout Instability API (the same
// numbers Lighthouse reports as CLS), on the pages that stream their content in: the home screen, the 40-member stokvel
// and its long contribution history.
//
// Good CLS is under 0.1. The limit here is lower (0.02) so a skeleton that is only a little off still fails.

test.use({ storageState: PEOPLE.mabuza.file })

const MABUZA = 'Mabuza Burial Society'
const LIMIT = 0.02

async function watchLayoutShifts(page: Page) {
  await page.addInitScript(() => {
    const w = window as unknown as { __cls: number; __shifts: { value: number; sources: string[] }[] }
    w.__cls = 0
    w.__shifts = []
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries() as unknown as { value: number; hadRecentInput: boolean; sources?: { node?: Element }[] }[]) {
        if (entry.hadRecentInput) continue // a shift right after a click or key press is the person's doing, not the page's
        w.__cls += entry.value
        w.__shifts.push({
          value: entry.value,
          sources: (entry.sources ?? []).map((s) => (s.node instanceof Element ? `${s.node.tagName.toLowerCase()}.${String(s.node.className).slice(0, 50)}` : '?')),
        })
      }
    }).observe({ type: 'layout-shift', buffered: true })
  })
}

/** Everything has arrived (no skeleton left), the browser has painted twice, and the shifts so far are added up. */
async function settledShifts(page: Page) {
  await expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
  await page.evaluate(() => new Promise<void>((done) => requestAnimationFrame(() => requestAnimationFrame(() => done()))))
  return page.evaluate(() => {
    const w = window as unknown as { __cls: number; __shifts: unknown[] }
    return { cls: w.__cls, shifts: w.__shifts }
  })
}

for (const [label, viewport] of [['phone', { width: 375, height: 667 }], ['desktop', { width: 1280, height: 800 }]] as const) {
  test.describe(`${label}`, () => {
    test.use({ viewport })

    test(`the home screen does not shift`, async ({ page }) => {
      await watchLayoutShifts(page)
      await page.goto('/dashboard/member')
      const { cls, shifts } = await settledShifts(page)
      expect(cls, JSON.stringify(shifts)).toBeLessThan(LIMIT)
    })

    test(`the 40-member stokvel page does not shift while its parts stream in`, async ({ page, request }) => {
      const id = await stokvelIdByName(request, 'mabuza', MABUZA)
      await watchLayoutShifts(page)
      await page.goto(`/stokvels/${id}`)
      await expect(page.getByRole('heading', { level: 1, name: MABUZA })).toBeVisible()
      const { cls, shifts } = await settledShifts(page)
      expect(cls, JSON.stringify(shifts)).toBeLessThan(LIMIT)
    })

    test(`the long contribution history does not shift`, async ({ page, request }) => {
      const id = await stokvelIdByName(request, 'mabuza', MABUZA)
      await watchLayoutShifts(page)
      await page.goto(`/stokvels/${id}/contributions`)
      await expect(page.getByRole('heading', { level: 3 }).first()).toBeVisible()
      const { cls, shifts } = await settledShifts(page)
      expect(cls, JSON.stringify(shifts)).toBeLessThan(LIMIT)
    })
  })
}

test('money is set in tabular figures: the same width whatever the digits, so a total does not change width as it grows', async ({ page, request }) => {
  const id = await stokvelIdByName(request, 'mabuza', MABUZA)
  await page.goto(`/stokvels/${id}`)
  await expect(page.getByRole('heading', { level: 1, name: MABUZA })).toBeVisible()
  // Measure the page's own font on a throwaway element inside the page (so it uses the same CSS and the loaded font).
  const widths = await page.evaluate(async () => {
    await document.fonts.ready
    const measure = (text: string) => {
      const span = document.createElement('span')
      span.textContent = text
      span.style.cssText = 'position:absolute;visibility:hidden;white-space:nowrap'
      document.querySelector('main')!.appendChild(span)
      const width = span.getBoundingClientRect().width
      span.remove()
      return width
    }
    return { ones: measure('R 1 111,11'), zeros: measure('R 0 000,00'), eights: measure('R 8 888,88') }
  })
  expect(widths.ones).toBeCloseTo(widths.zeros, 1)
  expect(widths.eights).toBeCloseTo(widths.zeros, 1)
})
