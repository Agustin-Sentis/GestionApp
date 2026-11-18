import type { EvolutionReport, PeriodTotals } from '../Services/api/api.ts'
import { currentMonthValue, monthName } from './format.ts'

/** One month of the chart and of the table. */
export interface EvolutionPoint {
  /** yyyy-MM */
  key: string
  /** Short, for the chart's axis: "nov 26". */
  label: string
  income: number
  expense: number
  balance: number
  /** The month still in progress: its figures are not final. */
  incomplete: boolean
  /** Some USD movements of this month were left out for lack of a rate: its figures are not complete either. */
  unconverted: boolean
}

export type EvolutionView =
  | { shape: 'empty'; message: string }
  | {
      shape: 'series'
      points: EvolutionPoint[]
      /** What the report says for the whole period, the month in progress included. */
      totals: PeriodTotals
      /** Per month. Leaves out the month in progress when there are others to average (see `note`). */
      average: PeriodTotals
      note: string | null
    }

const round2 = (value: number) => Math.round(value * 100) / 100

/**
 * What the evolution view draws, from the report. `currentMonth` (yyyy-MM) is the month still in progress: it is shown,
 * and counted in the totals, but it does not pull the average down, because it is half a month. When it is the only month
 * there is nothing else to average, so it counts.
 */
export function buildEvolutionView(report: EvolutionReport, currentMonth = currentMonthValue()): EvolutionView {
  if (report.months.every((m) => m.income === 0 && m.expense === 0)) {
    return { shape: 'empty', message: 'No hay movimientos en este período.' }
  }

  const points: EvolutionPoint[] = report.months.map((m) => {
    const key = `${m.year}-${String(m.month).padStart(2, '0')}`
    return {
      key,
      label: `${monthName(m.month).slice(0, 3)} ${String(m.year).slice(2)}`,
      income: m.income,
      expense: m.expense,
      balance: m.balance,
      incomplete: key === currentMonth,
      unconverted: m.hasUnconvertedUsd,
    }
  })

  const others = points.filter((p) => !p.incomplete)
  const skipsCurrent = others.length > 0 && others.length < points.length
  const counted = skipsCurrent ? others : points

  const income = round2(counted.reduce((sum, p) => sum + p.income, 0) / counted.length)
  const expense = round2(counted.reduce((sum, p) => sum + p.expense, 0) / counted.length)

  return {
    shape: 'series',
    points,
    totals: report.totals,
    average: { income, expense, balance: round2(income - expense) },
    note: skipsCurrent ? 'El promedio no cuenta el mes en curso, que todavía está incompleto.' : null,
  }
}
