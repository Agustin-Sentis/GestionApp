import { describe, expect, it } from 'vitest'
import type { EvolutionReport, MonthEvolution } from '../Services/api/api.ts'
import { buildEvolutionView } from './evolutionData.ts'

function month(year: number, monthNumber: number, income = 0, expense = 0, hasUnconvertedUsd = false): MonthEvolution {
  return { year, month: monthNumber, income, expense, balance: income - expense, hasUnconvertedUsd }
}

function report(months: MonthEvolution[]): EvolutionReport {
  const income = months.reduce((sum, m) => sum + m.income, 0)
  const expense = months.reduce((sum, m) => sum + m.expense, 0)
  return {
    from: `${months[0].year}-${String(months[0].month).padStart(2, '0')}`,
    to: `${months.at(-1)!.year}-${String(months.at(-1)!.month).padStart(2, '0')}`,
    currency: 'ARS',
    months,
    totals: { income, expense, balance: income - expense },
    pendingConversions: [],
    hasUnconvertedUsd: months.some((m) => m.hasUnconvertedUsd),
  }
}

/** The view, which for these tests has to be a series. */
function series(data: EvolutionReport, currentMonth: string) {
  const view = buildEvolutionView(data, currentMonth)
  if (view.shape !== 'series') throw new Error('expected a series')
  return view
}

describe('buildEvolutionView', () => {
  it('says there is nothing to show when no month has movements', () => {
    const view = buildEvolutionView(report([month(2026, 9), month(2026, 10)]), '2026-11')

    expect(view).toEqual({ shape: 'empty', message: 'No hay movimientos en este período.' })
  })

  it('makes one point per month, in order, with a short label in Spanish', () => {
    const view = series(report([month(2025, 12, 100, 40), month(2026, 1, 200, 50), month(2026, 9, 0, 10)]), '2026-11')

    expect(view.points.map((p) => [p.key, p.label])).toEqual([
      ['2025-12', 'dic 25'],
      ['2026-01', 'ene 26'],
      ['2026-09', 'sep 26'],
    ])
    expect(view.points[0]).toMatchObject({ income: 100, expense: 40, balance: 60 })
  })

  it('carries the totals of the report as they come', () => {
    const view = series(report([month(2026, 9, 1000, 300), month(2026, 10, 500, 900)]), '2026-11')

    expect(view.totals).toEqual({ income: 1500, expense: 1200, balance: 300 })
  })

  it('averages the months of the period', () => {
    const view = series(report([month(2026, 8, 1000, 400), month(2026, 9, 2000, 800), month(2026, 10, 0, 0)]), '2026-11')

    expect(view.average).toEqual({ income: 1000, expense: 400, balance: 600 })
    expect(view.note).toBeNull()
    expect(view.points.every((p) => !p.incomplete)).toBe(true)
  })

  it('rounds the average to cents', () => {
    const view = series(report([month(2026, 8, 100, 0), month(2026, 9, 0, 0), month(2026, 10, 0, 0)]), '2026-12')

    expect(view.average.income).toBe(33.33)
  })

  it('leaves the month in progress out of the average, says so, and marks its point', () => {
    const view = series(report([month(2026, 9, 1000, 400), month(2026, 10, 3000, 600), month(2026, 11, 100, 50)]), '2026-11')

    expect(view.average).toEqual({ income: 2000, expense: 500, balance: 1500 })
    expect(view.note).toBe('El promedio no cuenta el mes en curso, que todavía está incompleto.')
    expect(view.points.map((p) => p.incomplete)).toEqual([false, false, true])
    // The totals still count it: it is real money
    expect(view.totals.income).toBe(4100)
  })

  it('averages the month in progress when it is the only month of the range', () => {
    const view = series(report([month(2026, 11, 800, 200)]), '2026-11')

    expect(view.average).toEqual({ income: 800, expense: 200, balance: 600 })
    expect(view.note).toBeNull()
    expect(view.points[0].incomplete).toBe(true)
  })

  it('averages a single past month as that month', () => {
    const view = series(report([month(2026, 5, 900, 300)]), '2026-11')

    expect(view.average).toEqual({ income: 900, expense: 300, balance: 600 })
  })

  it('keeps which months are incomplete for lack of a rate', () => {
    const view = series(report([month(2026, 9, 100, 50), month(2026, 10, 100, 50, true)]), '2026-11')

    expect(view.points.map((p) => p.unconverted)).toEqual([false, true])
  })

  it('reads a month in progress that is in the range by its position, whatever the year', () => {
    const view = series(report([month(2025, 11, 100, 10), month(2026, 11, 100, 10)]), '2026-11')

    expect(view.points.map((p) => p.incomplete)).toEqual([false, true])
  })
})
