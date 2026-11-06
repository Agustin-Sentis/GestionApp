import { describe, expect, it } from 'vitest'
import type { PreviousMonth } from './api.ts'
import { compareAmount, comparePercent, compareWithPrevious, NO_COMPARISONS } from './comparison.ts'

// Intl separates the symbol from the number with a non-breaking space: compare with a normal one
const plain = (text: string) => text.replace(/ /g, ' ')

const previous = (overrides: Partial<PreviousMonth> = {}): PreviousMonth => ({
  year: 2026, month: 10, income: 1000, expense: 400, balance: 600, hasUnconvertedUsd: false, ...overrides,
})

describe('comparePercent', () => {
  it('says how much more it is, as a percentage of what it was', () => {
    const result = comparePercent(1125, 1000, 'octubre')!

    expect(result.direction).toBe('up')
    expect(result.short).toBe('▲ 12,5%')
    expect(result.sentence).toBe('12,5% más que en octubre')
  })

  it('says how much less it is', () => {
    const result = comparePercent(750, 1000, 'octubre')!

    expect(result.direction).toBe('down')
    expect(result.short).toBe('▼ 25%')
    expect(result.sentence).toBe('25% menos que en octubre')
  })

  it('says it is the same when nothing changed', () => {
    const result = comparePercent(1000, 1000, 'octubre')!

    expect(result.direction).toBe('same')
    expect(result.short).toBe('= igual')
    expect(result.sentence).toBe('Igual que en octubre')
  })

  it('counts a change too small to show (under 0,05%) as no change, instead of "0%" with an arrow', () => {
    expect(comparePercent(1000.01, 1000, 'octubre')!.direction).toBe('same')
    expect(comparePercent(999.99, 1000, 'octubre')!.direction).toBe('same')
  })

  it('rounds to one decimal', () => {
    expect(comparePercent(1012.345, 1000, 'octubre')!.short).toBe('▲ 1,2%')
  })

  it('can be more than double', () => {
    expect(comparePercent(3000, 1000, 'octubre')!.sentence).toBe('200% más que en octubre')
  })

  it('shows thousands with a dot, the Argentine way', () => {
    expect(comparePercent(1_000_000, 1000, 'octubre')!.short).toBe('▲ 99.900%')
  })

  it('has nothing to say when it was zero before: "up infinity percent" means nothing', () => {
    expect(comparePercent(500, 0, 'octubre')).toBeNull()
    expect(comparePercent(0, 0, 'octubre')).toBeNull()
  })

  it('has nothing to say for a negative base', () => {
    expect(comparePercent(500, -100, 'octubre')).toBeNull()
  })

  it('goes down to nothing as "100% menos"', () => {
    expect(comparePercent(0, 400, 'octubre')!.sentence).toBe('100% menos que en octubre')
  })
})

describe('compareAmount', () => {
  it('gives the difference in pesos, for the balance', () => {
    const result = compareAmount(750, 600, 'octubre')

    expect(result.direction).toBe('up')
    expect(plain(result.short)).toBe('▲ $ 150')
    expect(plain(result.sentence)).toBe('$ 150 más que en octubre')
  })

  it('works when the balance went negative', () => {
    const result = compareAmount(-200, 600, 'octubre')

    expect(result.direction).toBe('down')
    expect(plain(result.sentence)).toBe('$ 800 menos que en octubre')
  })

  it('keeps the cents when there are any', () => {
    expect(plain(compareAmount(600.5, 600, 'octubre').short)).toBe('▲ $ 0,50')
  })

  it('says it is the same when the difference is under one cent', () => {
    expect(compareAmount(600.004, 600, 'octubre').direction).toBe('same')
  })

  it('compares two negative balances correctly', () => {
    const result = compareAmount(-100, -300, 'octubre') // owes less than before

    expect(result.direction).toBe('up')
    expect(plain(result.sentence)).toBe('$ 200 más que en octubre')
  })
})

describe('compareWithPrevious', () => {
  const current = { income: 1200, expense: 300, balance: 900 }

  it('compares the three figures against the month before, naming it', () => {
    const result = compareWithPrevious(current, previous())

    expect(result.income?.sentence).toBe('20% más que en octubre')
    expect(result.expense?.sentence).toBe('25% menos que en octubre')
    expect(plain(result.balance!.sentence)).toBe('$ 300 más que en octubre')
  })

  it('names the month before correctly across the year', () => {
    const result = compareWithPrevious(current, previous({ year: 2025, month: 12 }))

    expect(result.income?.month).toBe('diciembre')
  })

  it('compares nothing when there is no previous month', () => {
    expect(compareWithPrevious(current, null)).toEqual(NO_COMPARISONS)
  })

  it('compares nothing when the previous month had no movements at all', () => {
    expect(compareWithPrevious(current, previous({ income: 0, expense: 0, balance: 0 }))).toEqual(NO_COMPARISONS)
  })

  it('compares nothing when the previous month is incomplete for lack of a USD rate: a wrong number is worse than none', () => {
    expect(compareWithPrevious(current, previous({ hasUnconvertedUsd: true }))).toEqual(NO_COMPARISONS)
  })

  it('skips only the figure that was zero before, and keeps the others', () => {
    const result = compareWithPrevious(current, previous({ income: 0, balance: -400 }))

    expect(result.income).toBeNull() // no income last month: no percentage
    expect(result.expense).not.toBeNull()
    expect(result.balance).not.toBeNull() // the balance is compared in pesos, so it always can be
  })
})
