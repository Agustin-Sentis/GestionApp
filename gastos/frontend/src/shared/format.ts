export function formatMoney(amount: number, currency = 'ARS'): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency }).format(amount)
}

/**
 * Like formatMoney, but without the ",00" when the amount is whole pesos: "$ 2.693.000" instead of "$ 2.693.000,00".
 * For the figures of a header, where room is tight. An amount with cents keeps them, so nothing is rounded away.
 */
export function formatMoneyCompact(amount: number, currency = 'ARS'): string {
  const whole = Number.isInteger(amount)
  return new Intl.NumberFormat('es-AR', {
    style: 'currency',
    currency,
    minimumFractionDigits: whole ? 0 : 2,
    maximumFractionDigits: 2,
  }).format(amount)
}

/** 45.678 → "45,7%"; 100 → "100%". Un decimal como máximo, con la coma del español. */
export function formatPercent(percent: number): string {
  return `${new Intl.NumberFormat('es-AR', { maximumFractionDigits: 1 }).format(percent)}%`
}

/** Fecha de hoy en hora local (toISOString devolvería la fecha UTC y se corre de día de noche). */
export function todayLocal(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** Mes actual con el formato de <input type="month">: yyyy-MM. */
export function currentMonthValue(): string {
  return todayLocal().slice(0, 7)
}

export function parseMonthValue(value: string): { year: number; month: number } {
  const [year, month] = value.split('-').map(Number)
  return { year, month }
}

/** The first and last month the server accepts for the summary and the list. */
export const MIN_MONTH = '2000-01'
export const MAX_MONTH = '2100-12'

/**
 * The month `delta` months before (negative) or after (positive) a yyyy-MM value, crossing years correctly.
 * Stays where it is rather than leaving the range the server accepts.
 */
export function addMonths(value: string, delta: number): string {
  const { year, month } = parseMonthValue(value)
  const index = year * 12 + (month - 1) + delta
  const result = `${Math.floor(index / 12)}-${String((index % 12) + 1).padStart(2, '0')}`
  return result < MIN_MONTH || result > MAX_MONTH ? value : result
}

/**
 * The date a new transaction starts with while the user is looking at `month`: today if that is the current month,
 * otherwise a day inside the month shown (the last one for a past month, the first for a future one), so what they
 * add lands where they are looking.
 */
export function defaultDateForMonth(month: string, today = todayLocal()): string {
  const current = today.slice(0, 7)
  if (month === current) return today
  if (month > current) return `${month}-01`
  const { year, month: monthNumber } = parseMonthValue(month)
  const lastDay = new Date(year, monthNumber, 0).getDate() // day 0 of the next month is the last of this one
  return `${month}-${String(lastDay).padStart(2, '0')}`
}

/** 10 → "octubre" */
export function monthName(month: number): string {
  return new Date(2000, month - 1, 1).toLocaleDateString('es-AR', { month: 'long' })
}

/** yyyy-MM → "noviembre de 2026" */
export function formatMonth(value: string): string {
  const { year, month } = parseMonthValue(value)
  return new Date(year, month - 1, 1).toLocaleDateString('es-AR', { month: 'long', year: 'numeric' })
}

/** yyyy-MM-dd → dd/MM/yyyy */
export function formatDate(iso: string): string {
  const [y, m, d] = iso.split('-')
  return `${d}/${m}/${y}`
}

/**
 * yyyy-MM-dd → a Date at local midnight, or undefined when it is empty or not a real day ("2026-02-31").
 * Built from its parts, because `new Date("2026-11-17")` is UTC midnight and shows the day before in Argentina.
 */
export function parseDateValue(value: string): Date | undefined {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) return undefined
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])]
  const date = new Date(year, month - 1, day)
  const isRealDay = date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
  return isRealDay ? date : undefined
}

/** A Date → yyyy-MM-dd with its local day (the opposite of parseDateValue). */
export function toDateValue(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}
