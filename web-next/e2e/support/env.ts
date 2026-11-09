// What the end-to-end tests need from the environment. Nothing secret is written in any file.

/** Where the API is (the tests call it directly only to prepare data, never to test the app's screens). */
export const API = process.env.API_BASE_URL ?? 'http://localhost:8080'

/** The demo accounts (sipho@, lerato@, thandi@tallyvel.test) all use the demo password. */
export function demoPassword(): string {
  const password = process.env.E2E_DEMO_PASSWORD ?? process.env.SEED_DEMO_PASSWORD
  if (!password) {
    throw new Error(
      'Set E2E_DEMO_PASSWORD to the demo password (SEED_DEMO_PASSWORD in your .env) before running the end-to-end tests.',
    )
  }
  return password
}

export const PEOPLE = {
  lerato: { email: 'lerato@tallyvel.test', file: 'e2e/.auth/lerato.json', tokenFile: 'e2e/.auth/lerato.token.json' },
  sipho: { email: 'sipho@tallyvel.test', file: 'e2e/.auth/sipho.json', tokenFile: 'e2e/.auth/sipho.token.json' },
  thandi: { email: 'thandi@tallyvel.test', file: 'e2e/.auth/thandi.json', tokenFile: 'e2e/.auth/thandi.token.json' },
} as const

export type Person = keyof typeof PEOPLE
