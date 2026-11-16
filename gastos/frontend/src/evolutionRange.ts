import { MAX_MONTH, MIN_MONTH, parseMonthValue } from './format.ts'

/** The longest range the server answers, both months included. */
export const MAX_RANGE_MONTHS = 24

/** The shortcuts, as they are written in the address (?rango=6). "anio" is January up to the month on screen. */
export type RangePreset = '3' | '6' | '12' | 'anio'

/** A span of months, both included. Both ends are yyyy-MM. */
export interface MonthRange {
  from: string
  to: string
}

export interface ResolvedRange {
  /** What to ask the server for. Null when the range the user wrote cannot be asked for (see `error`). */
  range: MonthRange | null
  error: string | null
  /** Which shortcut is on, or 'custom' for a range written by hand. */
  mode: RangePreset | 'custom'
}

/** The URL parameters that hold the range written by hand, and the shortcut. */
export const FROM_PARAM = 'desde'
export const TO_PARAM = 'hasta'
export const PRESET_PARAM = 'rango'

const DEFAULT_PRESET: RangePreset = '6'
const PRESETS: readonly RangePreset[] = ['3', '6', '12', 'anio']
const MONTH_FORMAT = /^\d{4}-(0[1-9]|1[0-2])$/

const isPreset = (value: string | null): value is RangePreset => PRESETS.includes(value as RangePreset)

/** Something the server would accept as a month: well written and inside the years the app supports. */
const isAcceptedMonth = (value: string | null): value is string =>
  value !== null && MONTH_FORMAT.test(value) && value >= MIN_MONTH && value <= MAX_MONTH

/** Months since year 0, so that two months can be subtracted across years. */
function monthIndex(value: string): number {
  const { year, month } = parseMonthValue(value)
  return year * 12 + (month - 1)
}

function monthOfIndex(index: number): string {
  return `${Math.floor(index / 12)}-${String((index % 12) + 1).padStart(2, '0')}`
}

/** How many months a range covers, counting both ends. */
export function monthCount(range: MonthRange): number {
  return monthIndex(range.to) - monthIndex(range.from) + 1
}

/** A shortcut, ending at the month on screen. It never starts before the first month the app accepts. */
function presetRange(preset: RangePreset, selectedMonth: string): MonthRange {
  if (preset === 'anio') return { from: `${selectedMonth.slice(0, 4)}-01`, to: selectedMonth }

  const from = monthOfIndex(monthIndex(selectedMonth) - (Number(preset) - 1))
  return { from: from < MIN_MONTH ? MIN_MONTH : from, to: selectedMonth }
}

/**
 * The range the evolution view shows, from the address and the month on screen. A range written by hand (desde and hasta,
 * both) wins over a shortcut and does not depend on the month on screen; anything that is not understood falls back to
 * the last 6 months, so a broken link still opens something useful.
 */
export function resolveRange(params: URLSearchParams, selectedMonth: string): ResolvedRange {
  const from = params.get(FROM_PARAM)
  const to = params.get(TO_PARAM)

  if (isAcceptedMonth(from) && isAcceptedMonth(to)) {
    const range = { from, to }
    if (from > to) return { range: null, error: 'El mes inicial no puede ser posterior al final.', mode: 'custom' }
    if (monthCount(range) > MAX_RANGE_MONTHS) {
      return { range: null, error: `El rango no puede superar los ${MAX_RANGE_MONTHS} meses.`, mode: 'custom' }
    }
    return { range, error: null, mode: 'custom' }
  }

  const requested = params.get(PRESET_PARAM)
  const preset = isPreset(requested) ? requested : DEFAULT_PRESET
  return { range: presetRange(preset, selectedMonth), error: null, mode: preset }
}
