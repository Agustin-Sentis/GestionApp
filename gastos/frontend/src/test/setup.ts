import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

// Testing Library only cleans up on its own when Vitest globals are on; they are off, so it is done here.
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals() // puts the real fetch back after a test replaced it
  vi.useRealTimers()
  localStorage.clear() // the session lives there: no test may inherit another one's login
})
