import fs from 'node:fs'
import path from 'node:path'
import { expect, test as setup } from '@playwright/test'
import { API, demoPassword, PEOPLE, type Person } from './support/env'

// Signs in ONCE per person, through the real sign-in page, and saves the session. Every test then starts already
// signed in (test.use({ storageState })), so no test signs in again and the API's login rate limit is used 3 times
// per run, not once per test. It runs at the start of every run because refresh tokens rotate: a session saved by an
// earlier run would be stale. The saved files hold session cookies and API tokens, so e2e/.auth/ is git-ignored.

for (const person of Object.keys(PEOPLE) as Person[]) {
  setup(`sign in as ${person}`, async ({ page, request }) => {
    const { email, file, tokenFile } = PEOPLE[person]
    fs.mkdirSync(path.dirname(file), { recursive: true })

    await page.goto('/login')
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Password').fill(demoPassword())
    await page.getByRole('button', { name: 'Sign in' }).click()
    await expect(page).toHaveURL(/\/dashboard\/member/)
    await page.context().storageState({ path: file })

    // Tests that need to PREPARE data (a stokvel with an open cycle) talk to the API as this person. The token is
    // short-lived and lives only in this git-ignored folder.
    const login = await request.post(`${API}/api/v1/auth/login`, { data: { email, password: demoPassword() } })
    expect(login.ok(), 'the API accepts the demo account').toBeTruthy()
    const { accessToken } = (await login.json()) as { accessToken: string }
    fs.writeFileSync(tokenFile, JSON.stringify({ accessToken }))
  })
}
