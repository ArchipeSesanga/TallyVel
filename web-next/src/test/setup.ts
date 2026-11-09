// Runs before every test file. Only component tests (jsdom) need the browser extras.
import { afterEach } from 'vitest'

if (typeof document !== 'undefined') {
  await import('@testing-library/jest-dom/vitest') // adds toBeInTheDocument, toHaveAccessibleName, ...
  const { cleanup } = await import('@testing-library/react')
  afterEach(() => cleanup()) // unmount what a test rendered, so tests can't see each other's pages
}
