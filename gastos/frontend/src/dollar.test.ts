import { describe, expect, it } from 'vitest'
import { DOLLAR_TYPES } from './api.ts'
import { DOLLAR_HINTS, DOLLAR_LABELS, DOLLAR_NAMES } from './dollar.ts'

describe('the names of the dollars', () => {
  it('has a name, a label and a hint for each of the three dollars, and nothing else', () => {
    expect(DOLLAR_TYPES).toEqual(['Official', 'Blue', 'Mep'])
    for (const type of DOLLAR_TYPES) {
      expect(DOLLAR_NAMES[type]).toBeTruthy()
      expect(DOLLAR_LABELS[type]).toBeTruthy()
      expect(DOLLAR_HINTS[type]).toBeTruthy()
    }
    expect(Object.keys(DOLLAR_NAMES)).toEqual([...DOLLAR_TYPES])
  })

  it('writes them the way they are said in Argentina', () => {
    expect(DOLLAR_NAMES).toEqual({ Official: 'oficial', Blue: 'blue', Mep: 'MEP' })
    expect(DOLLAR_LABELS).toEqual({ Official: 'Oficial', Blue: 'Blue', Mep: 'MEP' })
  })

  it('explains each one in a short sentence that ends with a full stop', () => {
    for (const type of DOLLAR_TYPES) {
      expect(DOLLAR_HINTS[type]).toMatch(/\.$/)
    }
    expect(DOLLAR_HINTS.Mep).toContain('bolsa') // the MEP dollar is also known as that
  })
})
