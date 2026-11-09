import { expect, test, type Page } from '@playwright/test'
import { createStokvelWithOpenCycle, stokvelIdByName, uniqueName } from './support/api'
import { PEOPLE } from './support/env'

// Keyboard and layout checks that axe cannot make. Each is a WCAG 2.2 criterion, named in its test.
// They run at a PHONE size, where a sticky header takes the biggest share of the screen.

test.use({ storageState: PEOPLE.mabuza.file, viewport: { width: 375, height: 667 } })

const MABUZA = 'Mabuza Burial Society' // the 40-member stokvel from the development seed: hundreds of contributions

async function openLongContributionList(page: Page, request: Parameters<typeof stokvelIdByName>[0]) {
  const id = await stokvelIdByName(request, 'mabuza', MABUZA)
  await page.goto(`/stokvels/${id}/contributions`)
  await expect(page.getByRole('list').filter({ has: page.getByRole('listitem') }).first()).toBeVisible()
  await expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
}

/** Where the focused element is, and where the sticky header ends, both measured in the browser right now. */
async function focusAndHeader(page: Page) {
  return page.evaluate(() => {
    const describe = (e: Element) => `${e.tagName.toLowerCase()}${e.getAttribute('name') ? `[name=${e.getAttribute('name')}]` : ''}${e.getAttribute('type') ? `[type=${e.getAttribute('type')}]` : ''} "${(e.getAttribute('aria-label') ?? e.textContent ?? '').trim().slice(0, 30)}"`
    const el = document.activeElement
    const header = document.querySelector('header')
    if (el === null || el === document.body) return null
    const r = el.getBoundingClientRect()
    const visible = !!(el as HTMLElement).checkVisibility?.({ visibilityProperty: true })
    return {
      what: describe(el),
      inHeader: el.closest('header') !== null, // the header's own controls are not "under" it
      top: r.top, bottom: r.bottom, left: r.left, right: r.right, width: r.width, height: r.height, visible,
      viewportWidth: window.innerWidth,
      headerBottom: header === null ? 0 : header.getBoundingClientRect().bottom,
      viewportHeight: window.innerHeight,
    }
  })
}

test('2.4.11 Focus not obscured: tabbing down a long contribution list never puts the focused item under the sticky header', async ({ page, request }) => {
  await openLongContributionList(page, request)

  const problems: string[] = []
  let stops = 0
  for (let i = 0; i < 60; i++) {
    await page.keyboard.press('Tab')
    const f = await focusAndHeader(page)
    if (f === null) continue
    stops++
    // Nothing the person tabs to may be hidden (a closed drawer's links) or have no size.
    if (!f.visible || f.width === 0 || f.height === 0) problems.push(`#${i} ${f.what}: focus is on something that is not visible`)
    // Not under the header, and not below the bottom edge of the screen.
    else if (f.left < -1 || f.right > f.viewportWidth + 1) problems.push(`#${i} ${f.what}: focus is off the side of the screen (${Math.round(f.left)} to ${Math.round(f.right)})`)
    else if (!f.inHeader && f.top < f.headerBottom - 1) problems.push(`#${i} ${f.what}: top ${Math.round(f.top)} is under the sticky header (ends at ${Math.round(f.headerBottom)})`)
    else if (f.bottom > f.viewportHeight + 1) problems.push(`#${i} ${f.what}: bottom ${Math.round(f.bottom)} is below the screen (${f.viewportHeight})`)
  }
  expect(stops).toBeGreaterThan(10)
  expect(problems).toEqual([])
})

test('2.4.11 Focus not obscured: going back up with shift+tab does not hide it either', async ({ page, request }) => {
  await openLongContributionList(page, request)
  // Down first, so the way back up starts well below the top (a fixed number of stops: the same every run).
  for (let i = 0; i < 20; i++) await page.keyboard.press('Tab')

  const problems: string[] = []
  for (let i = 0; i < 20; i++) {
    await page.keyboard.press('Shift+Tab')
    const f = await focusAndHeader(page)
    if (f === null) continue
    if (f.visible && !f.inHeader && f.top < f.headerBottom - 1) problems.push(`#${i} ${f.what}: top ${Math.round(f.top)} is under the sticky header (ends at ${Math.round(f.headerBottom)})`)
  }
  expect(problems).toEqual([])
})

test('2.4.7 / 2.4.13 Focus visible: every control we tab to shows an outline or ring', async ({ page, request }) => {
  await openLongContributionList(page, request)
  const missing: string[] = []
  for (let i = 0; i < 25; i++) {
    await page.keyboard.press('Tab')
    const seen = await page.evaluate(() => {
      const el = document.activeElement as HTMLElement | null
      if (el === null || el === document.body || !el.checkVisibility()) return null
      // A date field has Tab stops INSIDE the browser's own control (the calendar button). For those the field itself is
      // the active element but is not :focus-visible; globals.css draws their ring with ::-webkit-calendar-picker-indicator.
      if (el.getAttribute('type') === 'date' && !el.matches(':focus-visible')) return null
      const s = getComputedStyle(el)
      const outline = s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) >= 1
      // Tailwind's ring is a box-shadow; a bare box-shadow of "none" means nothing is drawn.
      const ring = s.boxShadow !== 'none' && /\d/.test(s.boxShadow)
      // A wrapper can draw it (focus-within), which is how the search box does it.
      const wrapper = el.closest('[class*="focus-within"]')
      const what = `${el.tagName.toLowerCase()}${el.getAttribute('name') ? `[name=${el.getAttribute('name')}]` : ''}${el.getAttribute('type') ? `[type=${el.getAttribute('type')}]` : ''} "${(el.getAttribute('aria-label') ?? el.textContent ?? '').trim().slice(0, 30)}"`
      return { what, drawn: outline || ring || wrapper !== null }
    })
    if (seen !== null && !seen.drawn) missing.push(seen.what)
  }
  expect(missing).toEqual([])
})

test.describe('2.5.8 Target size (minimum): every control is at least 24 x 24 CSS px', () => {
  /** Buttons, links that are not inside a sentence, fields. Returns the ones smaller than 24 x 24. */
  async function tooSmall(page: Page) {
    return page.evaluate(() => {
      const small: string[] = []
      for (const el of document.querySelectorAll<HTMLElement>('button, a[href], input:not([type=hidden]), select, textarea, [role=button], summary')) {
        if (!el.checkVisibility({ visibilityProperty: true })) continue
        const r = el.getBoundingClientRect()
        if (r.width === 0 || r.height === 0) continue
        // WCAG exception: a link inside a sentence ("inline") is exempt. Block-level and flex links are not.
        const inline = el.tagName === 'A' && getComputedStyle(el).display === 'inline'
        if (inline) continue
        if (r.width < 24 || r.height < 24) small.push(`${el.tagName.toLowerCase()}${el.getAttribute('type') ? `[type=${el.getAttribute('type')}]` : ''} "${(el.textContent || el.getAttribute('aria-label') || el.getAttribute('name') || '').trim().slice(0, 30)}" is ${Math.round(r.width)} x ${Math.round(r.height)}`)
      }
      return small
    })
  }

  test('the contribution list on a phone', async ({ page, request }) => {
    await openLongContributionList(page, request)
    expect(await tooSmall(page)).toEqual([])
  })

  test('the home screen on a phone', async ({ page }) => {
    await page.goto('/dashboard/member')
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
    expect(await tooSmall(page)).toEqual([])
  })

  test('a stokvel page on a phone (proof controls, role pickers, close and leave)', async ({ page, request }, testInfo) => {
    await page.goto('/stokvels')
    const stokvel = await createStokvelWithOpenCycle(request, 'mabuza', uniqueName('A11y target', testInfo.workerIndex), 50_000)
    await page.goto(`/stokvels/${stokvel.id}`)
    await expect(page.getByRole('heading', { level: 1, name: stokvel.name })).toBeVisible()
    await expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
    expect(await tooSmall(page)).toEqual([])
  })
})
