import '@testing-library/jest-dom/vitest'
import { afterEach, vi } from 'vitest'
import { cleanup } from '@testing-library/react'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  // Tests stub fetch and localStorage; clearing between them keeps one test's
  // state from leaking into the next.
  localStorage.clear()
})
