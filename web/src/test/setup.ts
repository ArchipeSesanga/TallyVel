import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Take rendered components off the page after every test, so one test can't see another's.
afterEach(() => {
  cleanup()
})
