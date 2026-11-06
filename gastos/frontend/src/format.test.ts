import { describe, expect, it, vi } from 'vitest'
import { addMonths, currentMonthValue, defaultDateForMonth, formatDate, formatMoney, formatMoneyCompact, formatMonth, parseMonthValue, todayLocal } from './format.ts'

// Intl separates the symbol from the number with a non-breaking space: compare with a normal one
const plain = (text: string) => text.replace(/ /g, ' ')

describe('formatMoney', () => {
  it('formats pesos the Argentine way (dot for thousands, comma for decimals)', () => {
    expect(plain(formatMoney(1234.5))).toBe('$ 1.234,50')
  })

  it('formats dollars with their own symbol', () => {
    expect(plain(formatMoney(10, 'USD'))).toBe('US$ 10,00')
  })

  it('keeps the sign of a negative balance', () => {
    expect(plain(formatMoney(-500))).toMatch(/^-\s?\$\s?500,00$/)
  })
})

describe('formatMoneyCompact', () => {
  it('leaves out the cents of whole pesos', () => {
    expect(plain(formatMoneyCompact(2693000))).toBe('$ 2.693.000')
    expect(plain(formatMoneyCompact(0))).toBe('$ 0')
  })

  it('keeps the cents when there are any, always with two digits', () => {
    expect(plain(formatMoneyCompact(1234.5))).toBe('$ 1.234,50')
    expect(plain(formatMoneyCompact(0.05))).toBe('$ 0,05')
  })

  it('handles dollars and negative amounts', () => {
    expect(plain(formatMoneyCompact(10, 'USD'))).toBe('US$ 10')
    expect(plain(formatMoneyCompact(-500))).toMatch(/^-\s?\$\s?500$/)
  })

  it('agrees with formatMoney on the digits it does show', () => {
    for (const amount of [1, 99.99, 1000000, 1234567.89]) {
      const compact = plain(formatMoneyCompact(amount))
      expect(plain(formatMoney(amount))).toContain(compact)
    }
  })
})

describe('addMonths', () => {
  it.each([
    ['2026-11', 1, '2026-12'],
    ['2026-11', -1, '2026-10'],
    ['2026-12', 1, '2027-01'], // crosses into the next year
    ['2026-01', -1, '2025-12'], // crosses into the previous year
    ['2026-01', -12, '2025-01'],
    ['2026-03', 14, '2027-05'],
    ['2026-03', 0, '2026-03'],
  ])('%s %i months → %s', (month, delta, expected) => {
    expect(addMonths(month, delta)).toBe(expected)
  })

  it('does not leave the months the server accepts', () => {
    expect(addMonths('2000-01', -1)).toBe('2000-01')
    expect(addMonths('2100-12', 1)).toBe('2100-12')
    expect(addMonths('2100-12', -1)).toBe('2100-11')
  })
})

describe('defaultDateForMonth', () => {
  const today = '2026-11-06'

  it('is today in the current month', () => {
    expect(defaultDateForMonth('2026-11', today)).toBe('2026-11-06')
  })

  it('is the last day of a past month', () => {
    expect(defaultDateForMonth('2026-10', today)).toBe('2026-10-31')
    expect(defaultDateForMonth('2026-04', today)).toBe('2026-04-30')
    expect(defaultDateForMonth('2025-12', today)).toBe('2025-12-31')
  })

  it('is the last day of February, leap or not', () => {
    expect(defaultDateForMonth('2026-02', today)).toBe('2026-02-28')
    expect(defaultDateForMonth('2024-02', today)).toBe('2024-02-29') // 2024 is a leap year and already past
  })

  it('is the first day of a future month', () => {
    expect(defaultDateForMonth('2026-12', today)).toBe('2026-12-01')
    expect(defaultDateForMonth('2027-01', today)).toBe('2027-01-01')
  })

  it('compares months across years', () => {
    expect(defaultDateForMonth('2025-12', '2026-01-15')).toBe('2025-12-31')
    expect(defaultDateForMonth('2026-01', '2025-12-15')).toBe('2026-01-01')
  })
})

describe('formatMonth', () => {
  it('writes the month name in Spanish', () => {
    expect(formatMonth('2026-03')).toBe('marzo de 2026')
    expect(formatMonth('2026-12')).toBe('diciembre de 2026')
  })
})

describe('dates', () => {
  it('uses the local day, not the UTC one, late at night', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 10, 5, 23, 30)) // 5 Nov 23:30 local time

    expect(todayLocal()).toBe('2026-11-05')
  })

  it('pads single-digit months and days', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 0, 3, 9, 0))

    expect(todayLocal()).toBe('2026-01-03')
  })

  it('gives the current month in the format of a month input', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 10, 5, 12, 0))

    expect(currentMonthValue()).toBe('2026-11')
  })

  it('parses a month input value', () => {
    expect(parseMonthValue('2026-03')).toEqual({ year: 2026, month: 3 })
  })

  it('formats an ISO date as day/month/year without going through Date (no time zone shift)', () => {
    expect(formatDate('2026-11-05')).toBe('05/11/2026')
  })
})
