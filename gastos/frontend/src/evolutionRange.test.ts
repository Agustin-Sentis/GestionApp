import { describe, expect, it } from 'vitest'
import { MAX_RANGE_MONTHS, monthCount, resolveRange } from './evolutionRange.ts'

const params = (query: string) => new URLSearchParams(query)

describe('resolveRange: the shortcuts', () => {
  it('opens with the last 6 months, ending at the month on screen', () => {
    expect(resolveRange(params(''), '2026-11')).toEqual({
      range: { from: '2026-06', to: '2026-11' },
      error: null,
      mode: '6',
    })
  })

  it.each([
    ['3', '2026-09'],
    ['6', '2026-06'],
    ['12', '2025-12'],
  ])('rango=%s ends at the month on screen and starts %s', (rango, from) => {
    const result = resolveRange(params(`rango=${rango}`), '2026-11')

    expect(result.range).toEqual({ from, to: '2026-11' })
    expect(result.mode).toBe(rango)
    expect(result.error).toBeNull()
  })

  it('"this year" goes from January of the year on screen to the month on screen', () => {
    expect(resolveRange(params('rango=anio'), '2026-03')).toEqual({
      range: { from: '2026-01', to: '2026-03' },
      error: null,
      mode: 'anio',
    })
  })

  it('follows the month on screen, crossing years', () => {
    expect(resolveRange(params('rango=6'), '2026-02').range).toEqual({ from: '2025-09', to: '2026-02' })
  })

  it('never goes before the first month the app accepts', () => {
    expect(resolveRange(params('rango=12'), '2000-03').range).toEqual({ from: '2000-01', to: '2000-03' })
  })

  it('can end at the last month the app accepts', () => {
    expect(resolveRange(params('rango=3'), '2100-12').range).toEqual({ from: '2100-10', to: '2100-12' })
  })
})

describe('resolveRange: a range of the user', () => {
  it('uses desde and hasta, whatever month is on screen', () => {
    expect(resolveRange(params('desde=2026-03&hasta=2026-09&rango=3'), '2026-11')).toEqual({
      range: { from: '2026-03', to: '2026-09' },
      error: null,
      mode: 'custom',
    })
  })

  it('accepts a single month', () => {
    const result = resolveRange(params('desde=2026-05&hasta=2026-05'), '2026-11')

    expect(result.range).toEqual({ from: '2026-05', to: '2026-05' })
    expect(result.error).toBeNull()
  })

  it('accepts exactly the longest range', () => {
    const result = resolveRange(params('desde=2025-01&hasta=2026-12'), '2026-11')

    expect(result.range).toEqual({ from: '2025-01', to: '2026-12' })
    expect(monthCount(result.range!)).toBe(MAX_RANGE_MONTHS)
  })

  it('says so, and asks for nothing, when it starts after it ends', () => {
    expect(resolveRange(params('desde=2026-09&hasta=2026-03'), '2026-11')).toEqual({
      range: null,
      error: 'El mes inicial no puede ser posterior al final.',
      mode: 'custom',
    })
  })

  it('says so, and asks for nothing, when it is longer than 24 months', () => {
    expect(resolveRange(params('desde=2024-12&hasta=2026-12'), '2026-11')).toEqual({
      range: null,
      error: 'El rango no puede superar los 24 meses.',
      mode: 'custom',
    })
  })
})

describe('resolveRange: nonsense in the address', () => {
  it.each([
    'desde=abc&hasta=2026-09',
    'desde=2026-03',
    'hasta=2026-09',
    'desde=&hasta=',
    'desde=2026-13&hasta=2026-14',
    'desde=1999-12&hasta=2000-02',
    'desde=2100-12&hasta=2101-01',
    'rango=99',
    'rango=',
    'rango=anio2',
  ])('falls back to the last 6 months, without an error: %s', (query) => {
    expect(resolveRange(params(query), '2026-11')).toEqual({
      range: { from: '2026-06', to: '2026-11' },
      error: null,
      mode: '6',
    })
  })
})

describe('monthCount', () => {
  it('counts both ends', () => {
    expect(monthCount({ from: '2026-05', to: '2026-05' })).toBe(1)
    expect(monthCount({ from: '2026-06', to: '2026-11' })).toBe(6)
  })

  it('crosses years', () => {
    expect(monthCount({ from: '2025-11', to: '2026-02' })).toBe(4)
  })
})
