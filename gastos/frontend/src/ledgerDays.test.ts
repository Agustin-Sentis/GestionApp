import { describe, expect, it } from 'vitest'
import { dayMarker, groupByDay } from './ledgerDays.ts'

const on = (date: string, id: string) => ({ date, id })

describe('groupByDay', () => {
  it('joins the movements of the same day and keeps the order of the list', () => {
    const groups = groupByDay([on('2026-11-17', 'a'), on('2026-11-17', 'b'), on('2026-11-16', 'c'), on('2026-11-14', 'd')])

    expect(groups.map((g) => [g.date, g.items.map((i) => i.id)])).toEqual([
      ['2026-11-17', ['a', 'b']],
      ['2026-11-16', ['c']],
      ['2026-11-14', ['d']],
    ])
  })

  it('does not reorder: the same date apart in the list makes two groups', () => {
    const groups = groupByDay([on('2026-11-17', 'a'), on('2026-11-16', 'b'), on('2026-11-17', 'c')])

    expect(groups.map((g) => g.items.map((i) => i.id))).toEqual([['a'], ['b'], ['c']])
  })

  it('is empty for an empty list', () => {
    expect(groupByDay([])).toEqual([])
  })
})

describe('dayMarker', () => {
  it('writes the day without a leading zero and the month in three letters', () => {
    expect(dayMarker('2026-11-05', 2026)).toEqual({ day: '5', month: 'nov', year: null })
    expect(dayMarker('2026-09-17', 2026)).toEqual({ day: '17', month: 'sep', year: null })
  })

  it('adds the year, in two digits, when it is not the current one', () => {
    expect(dayMarker('2025-12-31', 2026)).toEqual({ day: '31', month: 'dic', year: '25' })
    expect(dayMarker('2027-01-01', 2026)).toEqual({ day: '1', month: 'ene', year: '27' })
  })
})
