import { defineConfig, devices } from '@playwright/test'

// End-to-end tests against the WHOLE stack: the built Next.js app (`npm run start`, never `next dev`), the real API
// and a real PostgreSQL with the demo seed (`docker compose up`). Fast tests (Vitest) never need any of this.
//
//   1. docker compose up -d                      (API on :8080, with the demo seed)
//   2. npm run build
//   3. E2E_DEMO_PASSWORD=<SEED_DEMO_PASSWORD> npm run e2e
//
// See README, "Running the tests".

const PORT = Number(process.env.E2E_PORT ?? 3100)
const BASE_URL = `http://localhost:${PORT}`

// CI uses the Chromium that `npx playwright install` fetches. On a machine where that download is not possible,
// E2E_BROWSER_CHANNEL=chrome uses the Google Chrome that is already installed.
const browser = { ...devices['Desktop Chrome'], ...(process.env.E2E_BROWSER_CHANNEL ? { channel: process.env.E2E_BROWSER_CHANNEL } : {}) }

export default defineConfig({
  testDir: './e2e',
  // Tests do not depend on each other and make their own data, so they may run side by side. Two at a time is plenty
  // for this suite and gentle on the API.
  fullyParallel: true,
  workers: 2,
  forbidOnly: !!process.env.CI,
  retries: 0, // a test that needs a retry to pass is a flaky test: fix it, don't hide it
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : [['list']],
  expect: { timeout: 10_000 }, // web-first assertions wait up to this long for the page to show what is expected
  timeout: 60_000,
  use: {
    baseURL: BASE_URL,
    trace: 'retain-on-failure',
  },
  projects: [
    // Signs in once per person and saves the session to e2e/.auth/ (git-ignored). Every other test starts from it.
    { name: 'setup', testMatch: /auth\.setup\.ts/, use: browser },
    {
      name: 'chromium',
      use: browser,
      dependencies: ['setup'],
      testMatch: /.*\.spec\.ts/,
    },
  ],
  webServer: {
    command: `npm run start -- --port ${PORT}`,
    url: `${BASE_URL}/login`, // a public page: answers 200 as soon as the server is up
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      // The app calls the API from the server only. Where it is, and the key that seals the session cookie, come from
      // the environment (CI generates a fresh SESSION_SECRET for every run; locally .env.local provides them).
      ...(process.env.API_BASE_URL ? { API_BASE_URL: process.env.API_BASE_URL } : {}),
      ...(process.env.SESSION_SECRET ? { SESSION_SECRET: process.env.SESSION_SECRET } : {}),
      // A developer's .env.local may set this huge to test refreshing; here the normal margin applies, or every request
      // would rotate the refresh token and saved sessions would stop working.
      REFRESH_MARGIN_SECONDS: '60',
    },
  },
})
