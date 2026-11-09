import path from 'node:path'
import { defineConfig } from 'vitest/config'

// Unit and component tests. (End-to-end tests are Playwright's, in e2e/, and are not run by Vitest.)
export default defineConfig({
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, 'src'),
      // "server-only" throws when imported outside Next's server build. A test never runs server code in a
      // browser, but a component may IMPORT a Server Action file, so the guard is replaced by an empty module.
      'server-only': path.resolve(import.meta.dirname, 'src/test/serverOnlyStub.ts'),
    },
  },
  test: {
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['src/test/setup.ts'],
    // Component tests say `// @vitest-environment jsdom` at the top; plain logic tests run in Node (faster).
    environment: 'node',
    restoreMocks: true,
    // Node's own (experimental) localStorage prints a warning in every jsdom test; jsdom brings its own, which is the one used.
    execArgv: ['--no-experimental-webstorage'],
  },
})
