import { EXPENSE, INCOME } from './api.ts'
import type { Category, CategoryTotal, MonthlyReport } from './api.ts'
import { formatPercent } from './format.ts'

/** The charts the summary can show. The value is what goes in the URL (?grafico=destino). */
export type ChartKind = 'destino' | 'gastos' | 'ingresos' | 'ranking' | 'comparacion'

export const CHART_KINDS: readonly { value: ChartKind; label: string }[] = [
  { value: 'destino', label: 'Destino de los ingresos' },
  { value: 'gastos', label: 'Gastos por categoría' },
  { value: 'ingresos', label: 'Ingresos por categoría' },
  { value: 'ranking', label: 'Ranking de categorías' },
  { value: 'comparacion', label: 'Ingresos vs. gastos' },
]

export function isChartKind(value: string | null): value is ChartKind {
  return CHART_KINDS.some((kind) => kind.value === value)
}

/** One part of a chart: a category, the left-over money, or the tail folded into "Otras". */
export interface Slice {
  key: string
  name: string
  /** Pesos. */
  value: number
  /** Share of the chart's base (see ChartView.percentLabel), 0-100. Not rounded: the screen rounds it. */
  percent: number
  /** A CSS color: a chart token (--series-N, --chart-rest, ...) defined in index.css, so it follows light and dark mode. */
  color: string
  /** A short extra line for the table (for example "Gasto" in a ranking that mixes both types). */
  detail?: string
}

export type ChartView =
  | { shape: 'empty'; message: string }
  | { shape: 'donut' | 'bars'; slices: Slice[]; percentLabel: string; note: string | null }

/** Past this many parts a donut cannot be read: the smallest ones are folded into "Otras". */
export const MAX_DONUT_SLICES = 6

/** The validated categorical palette has eight hues; a ninth is never invented. */
const SERIES_COUNT = 8

const REST_COLOR = 'var(--chart-rest)'
const OTHER_COLOR = 'var(--chart-other)'
const RANKING_LIMIT = 10

export function percentOf(value: number, base: number): number {
  return base > 0 ? (value / base) * 100 : 0
}

/** The names of the user's categories in the order the server lists them. A category's color comes from its place here. */
export function categoryOrder(categories: Category[]): string[] {
  return categories.map((c) => normalize(c.name))
}

const normalize = (name: string) => name.trim().toLowerCase()

/**
 * The color of a category. It depends on the category itself (its place among all the user's categories), not on how big
 * it is or on what else is on screen, so "Comida" keeps its color in every chart and every month, and a filter never
 * repaints the survivors. When the categories could not be loaded the place in the chart is used instead.
 */
export function colorOf(name: string, order: string[], fallbackIndex: number): string {
  const found = order.indexOf(normalize(name))
  const index = found >= 0 ? found : order.length === 0 ? fallbackIndex : SERIES_COUNT
  return index < SERIES_COUNT ? `var(--series-${index + 1})` : OTHER_COLOR
}

const sum = (rows: { total: number }[]) => rows.reduce((acc, row) => acc + row.total, 0)

/**
 * Parts of a donut: the biggest first and, when there are too many, the smallest merged into "Otras".
 * `room` is how many parts fit (other pieces of the same donut, like "Sin gastar", take some).
 */
function toSlices(rows: CategoryTotal[], base: number, order: string[], room: number): Slice[] {
  const sorted = [...rows].sort((a, b) => b.total - a.total)
  const keep = sorted.length > room ? sorted.slice(0, room - 1) : sorted
  const folded = sorted.slice(keep.length)

  const slices: Slice[] = keep.map((row, index) => ({
    key: `${row.type}:${row.name}`,
    name: row.name,
    value: row.total,
    percent: percentOf(row.total, base),
    color: colorOf(row.name, order, index),
  }))

  if (folded.length > 0) {
    const value = sum(folded)
    slices.push({ key: 'other', name: 'Otras', value, percent: percentOf(value, base), color: OTHER_COLOR })
  }
  return slices
}

/** Which chart the summary opens with: where the income went, or, with no income, the expenses by category. */
export function defaultChartKind(report: MonthlyReport): ChartKind {
  return report.income > 0 ? 'destino' : 'gastos'
}

export function buildChartView(kind: ChartKind, report: MonthlyReport, order: string[]): ChartView {
  const expenses = report.byCategory.filter((c) => c.type === EXPENSE && c.total > 0)
  const incomes = report.byCategory.filter((c) => c.type === INCOME && c.total > 0)

  switch (kind) {
    case 'gastos': {
      if (expenses.length === 0) {
        return {
          shape: 'empty',
          message: incomes.length > 0 ? 'No hay gastos cargados en este mes.' : 'Todavía no hay movimientos cargados en este mes.',
        }
      }
      return {
        shape: 'donut',
        slices: toSlices(expenses, sum(expenses), order, MAX_DONUT_SLICES),
        percentLabel: '% de los gastos',
        note: null,
      }
    }

    case 'ingresos': {
      if (incomes.length === 0) return { shape: 'empty', message: 'No hay ingresos cargados en este mes.' }
      return {
        shape: 'donut',
        slices: toSlices(incomes, sum(incomes), order, MAX_DONUT_SLICES),
        percentLabel: '% de los ingresos',
        note: null,
      }
    }

    case 'destino': {
      const income = sum(incomes)
      if (income <= 0) {
        return { shape: 'empty', message: 'Todavía no hay ingresos en este mes: cargá uno para ver cómo se reparte.' }
      }

      // Every percentage is a share of the INCOME, so adding an income changes all of them
      const spent = sum(expenses)
      const unspent = income - spent
      const slices = toSlices(expenses, income, order, unspent > 0 ? MAX_DONUT_SLICES - 1 : MAX_DONUT_SLICES)
      if (unspent > 0) {
        slices.push({ key: 'rest', name: 'Sin gastar', value: unspent, percent: percentOf(unspent, income), color: REST_COLOR })
      }

      const spentPercent = formatPercent(percentOf(spent, income))
      const note =
        spent > income
          ? `Los gastos superan los ingresos: gastaste el ${spentPercent} de lo que ingresó. El gráfico muestra cómo se reparten los gastos; los porcentajes del detalle son sobre los ingresos.`
          : `Gastaste el ${spentPercent} de lo que ingresó este mes.`
      return { shape: 'donut', slices, percentLabel: '% de los ingresos', note }
    }

    case 'ranking': {
      const rows = report.byCategory.filter((c) => c.total > 0).sort((a, b) => b.total - a.total)
      if (rows.length === 0) return { shape: 'empty', message: 'Todavía no hay movimientos cargados en este mes.' }

      const totals = { [EXPENSE]: sum(expenses), [INCOME]: sum(incomes) }
      return {
        shape: 'bars',
        slices: rows.slice(0, RANKING_LIMIT).map((row, index) => ({
          key: `${row.type}:${row.name}`,
          name: row.name,
          value: row.total,
          percent: percentOf(row.total, totals[row.type]),
          color: colorOf(row.name, order, index),
          detail: row.type === EXPENSE ? 'Gasto' : 'Ingreso',
        })),
        percentLabel: '% de su tipo',
        note: rows.length > RANKING_LIMIT ? `Se muestran las ${RANKING_LIMIT} categorías con más movimiento.` : null,
      }
    }

    case 'comparacion': {
      const income = sum(incomes)
      const expense = sum(expenses)
      if (income <= 0 && expense <= 0) {
        return { shape: 'empty', message: 'Todavía no hay movimientos cargados en este mes.' }
      }

      return {
        shape: 'bars',
        slices: [
          { key: 'income', name: 'Ingresos', value: income, percent: income > 0 ? 100 : 0, color: 'var(--series-1)' },
          { key: 'expense', name: 'Gastos', value: expense, percent: percentOf(expense, income), color: 'var(--series-2)' },
        ],
        percentLabel: '% de los ingresos',
        note: income > 0 ? `Gastaste el ${formatPercent(percentOf(expense, income))} de lo que ingresó este mes.` : null,
      }
    }
  }
}
