import { expect, test, type Page } from '@playwright/test'
import { createInviteCode, createStokvelWithOpenCycle, uniqueName } from './support/api'
import { PEOPLE } from './support/env'

// What search engines and link previews may see. See README, "Metadata and indexing".

const metaContent = (page: Page, selector: string) => page.locator(`head meta${selector}`).getAttribute('content')

test.describe('signed in: every page says noindex', () => {
  test.use({ storageState: PEOPLE.lerato.file })

  for (const path of ['/dashboard/member', '/stokvels', '/activity', '/contributions', '/stokvels/new', '/join']) {
    test(`${path}`, async ({ page }) => {
      await page.goto(path)
      await expect(page.locator('head meta[name="robots"]')).toHaveAttribute('content', /noindex/)
    })
  }

  test('a stokvel page and its history', async ({ page, request }, testInfo) => {
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', uniqueName('Meta noindex', testInfo.workerIndex), 50_000)
    for (const path of [`/stokvels/${stokvel.id}`, `/stokvels/${stokvel.id}/contributions`]) {
      await page.goto(path)
      await expect(page.locator('head meta[name="robots"]')).toHaveAttribute('content', /noindex/)
    }
  })

  test('the pages have their own titles (WCAG 2.4.2), not one title for the whole site', async ({ page }) => {
    await page.goto('/stokvels')
    await expect(page).toHaveTitle('My stokvels · RondiTrack')
  })
})

test.describe('/bff/ routes and downloads carry X-Robots-Tag: noindex', () => {
  test.describe('signed in', () => {
    test.use({ storageState: PEOPLE.lerato.file })

    test('a JSON answer', async ({ request }) => {
      const response = await request.get('/bff/activity')
      expect(response.status()).toBe(200)
      expect(response.headers()['x-robots-tag']).toMatch(/noindex/)
    })

    test('a proof download route (here: a proof that does not exist, so a 404: the header is on every answer)', async ({ request }) => {
      const response = await request.get('/bff/stokvels/4ce16b73-25dd-4a88-b36c-e8b61a24bd65/contributions/c0000002-0000-4000-8000-000000000001/proof')
      expect(response.headers()['x-robots-tag']).toMatch(/noindex/)
    })
  })

  test.describe('signed out', () => {
    test.use({ storageState: { cookies: [], origins: [] } })

    test('even the 401', async ({ request }) => {
      const response = await request.get('/bff/activity')
      expect(response.status()).toBe(401)
      expect(response.headers()['x-robots-tag']).toMatch(/noindex/)
    })
  })
})

test.describe('robots.txt', () => {
  test('blocks nothing: signed-in pages, the invite page and /bff/ are all fetchable, so their noindex can be read', async ({ request }) => {
    const text = await (await request.get('/robots.txt')).text()
    const disallows = text.split('\n').filter((line) => /^\s*disallow\s*:\s*\S/i.test(line))
    expect(disallows).toEqual([])
    expect(text).toMatch(/Allow:\s*\//i)
  })
})

test.describe('signed out', () => {
  test.use({ storageState: { cookies: [], origins: [] } })

  test('the sign-in page is indexable and has a product title, a description and an image', async ({ page }) => {
    await page.goto('/login')
    await expect(page).toHaveTitle(/RondiTrack/)
    expect(await metaContent(page, '[name="description"]')).toMatch(/stokvel/i)
    expect(await metaContent(page, '[property="og:title"]')).toMatch(/RondiTrack/)
    expect(await metaContent(page, '[property="og:image"]')).toMatch(/\/opengraph-image/)
    expect(await metaContent(page, '[property="og:description"]')).not.toBe('')
    // Indexable = no robots tag that says noindex.
    await expect(page.locator('head meta[name="robots"][content*="noindex"]')).toHaveCount(0)
  })

  test('the preview picture can be fetched without signing in', async ({ request }) => {
    const response = await request.get('/opengraph-image')
    expect(response.status()).toBe(200)
    expect(response.headers()['content-type']).toContain('image/png')
  })
})

// A REAL invite, opened by someone who is not signed in, and by a link-preview bot that is never signed in.
test.describe('an invite opened signed out', () => {
  test.use({ storageState: { cookies: [], origins: [] } })

  async function realInvite(request: Parameters<typeof createStokvelWithOpenCycle>[0], workerIndex: number) {
    // Both the name and the amount are unusual on purpose, so finding them anywhere in the head would be unmistakable.
    const name = uniqueName('Zandile Secret Savers', workerIndex)
    const stokvel = await createStokvelWithOpenCycle(request, 'lerato', name, 123_456)
    const code = await createInviteCode(request, 'lerato', stokvel.id)
    return { name, code, amounts: ['1 234,56', '1234,56', '1234.56', '123456', '123 456', 'R 1'] }
  }

  test('the head has a title, a description and og: tags, and none of them holds the stokvel name or the amount', async ({ page, request }, testInfo) => {
    const invite = await realInvite(request, testInfo.workerIndex)
    const response = await page.goto(`/join?code=${invite.code}`)
    expect(response?.status()).toBe(200) // not a redirect to sign-in: a bot cannot follow one to the right page

    const head = await page.locator('head').evaluate((el) => ({
      title: document.title,
      metas: [...el.querySelectorAll('meta')].map((m) => ({ key: m.getAttribute('name') ?? m.getAttribute('property') ?? '', content: m.getAttribute('content') ?? '' })),
    }))
    const ogTags = head.metas.filter((m) => m.key.startsWith('og:') || m.key.startsWith('twitter:'))

    // It HAS what a preview needs...
    expect(head.title.length).toBeGreaterThan(5)
    expect(head.metas.find((m) => m.key === 'description')?.content.length).toBeGreaterThan(10)
    for (const key of ['og:title', 'og:description', 'og:image', 'og:type', 'og:site_name']) {
      expect(ogTags.find((m) => m.key === key)?.content, key).toBeTruthy()
    }
    expect(head.metas.find((m) => m.key === 'robots')?.content).toMatch(/noindex/)

    // ...and holds nothing about this invite. The whole head is searched: the title, every meta tag, every og: tag.
    const everything = [head.title, ...head.metas.map((m) => m.content)].join('\n')
    expect(everything).not.toContain(invite.name)
    expect(everything).not.toContain('Zandile')
    expect(everything).not.toContain(invite.code)
    for (const amount of invite.amounts) expect(everything, `the amount as "${amount}"`).not.toContain(amount)
  })

  test('a link-preview bot (WhatsApp user agent) gets the same: 200, a head in the raw HTML, and nothing about the stokvel', async ({ request }, testInfo) => {
    const invite = await realInvite(request, testInfo.workerIndex)
    const response = await request.get(`/join?code=${invite.code}`, { headers: { 'User-Agent': 'WhatsApp/2' }, maxRedirects: 0 })
    expect(response.status()).toBe(200)
    const html = await response.text()
    const head = html.slice(html.indexOf('<head>'), html.indexOf('</head>'))

    expect(head).toContain('<meta property="og:title"')
    expect(head).toContain('<meta property="og:image"')
    expect(head).toContain('<meta name="robots" content="noindex, nofollow"/>')
    expect(head).not.toContain(invite.code)
    // The whole response, not only the head, has no stokvel data: nothing was ever fetched for a request without a session.
    // (The body does repeat the visitor's own address inside the "Sign in" link, so that signing in comes back here.)
    expect(html).not.toContain(invite.name)
    expect(html).not.toContain('Zandile')
  })
})
