import { describe, expect, it } from 'vitest'
import { PANEL_PATH, returnPathFrom } from './returnPath.ts'

describe('returnPathFrom', () => {
  it('goes back to the page the visitor was on, with its month and filters', () => {
    expect(returnPathFrom({ from: '/panel?mes=2026-09&q=cafe' })).toBe('/panel?mes=2026-09&q=cafe')
  })

  it('goes to the dashboard when there is nothing to go back to', () => {
    expect(returnPathFrom(null)).toBe(PANEL_PATH)
    expect(returnPathFrom(undefined)).toBe(PANEL_PATH)
    expect(returnPathFrom({})).toBe(PANEL_PATH)
  })

  it.each([
    ['another site', 'https://evil.example/panel'],
    ['a protocol-relative address (also another site)', '//evil.example'],
    ['a backslash trick that browsers read as another site', '/\\evil.example'],
    ['a script', 'javascript:alert(1)'],
    ['a relative path', 'panel'],
    ['an empty string', ''],
    ['something that is not text', 42],
  ])('never leaves the app: %s', (_name, from) => {
    expect(returnPathFrom({ from })).toBe(PANEL_PATH)
  })

  it('does not send someone back to the sign-in form they just used', () => {
    expect(returnPathFrom({ from: '/login?modo=registro' })).toBe(PANEL_PATH)
  })
})
