import type { PreviousMonth } from './api.ts'
import { formatMoneyCompact, formatPercent, monthName } from './format.ts'

export type Direction = 'up' | 'down' | 'same'

/** How one figure of the month compares with the same figure of the month before, ready to show. */
export interface Comparison {
  direction: Direction
  /** "▲ 12,5%", "▼ $ 150.000" or "= igual": short enough for a narrow header. */
  short: string
  /** "12,5% más que en octubre": the whole sentence, for screen readers and the tooltip. */
  sentence: string
  /** "octubre" */
  month: string
}

const ARROWS: Record<Direction, string> = { up: '▲', down: '▼', same: '=' }

function build(direction: Direction, quantity: string, month: string): Comparison {
  const sentence =
    direction === 'up' ? `${quantity} más que en ${month}`
    : direction === 'down' ? `${quantity} menos que en ${month}`
    : `Igual que en ${month}`
  return {
    direction,
    short: direction === 'same' ? `${ARROWS.same} igual` : `${ARROWS[direction]} ${quantity}`,
    sentence,
    month,
  }
}

/** Rounded the way it is shown: a change too small to appear (under 0,05%) counts as no change. */
function direction(difference: number, shown: number): Direction {
  return shown === 0 || difference === 0 ? 'same' : difference > 0 ? 'up' : 'down'
}

/**
 * The change of an amount as a percentage of what it was. Null when it was nothing: "up infinity %" says nothing,
 * so there is no comparison to show.
 */
export function comparePercent(current: number, previous: number, month: string): Comparison | null {
  if (!(previous > 0)) return null

  const percent = Math.round(((current - previous) / previous) * 1000) / 10 // one decimal, signed
  const which = direction(current - previous, percent)
  return build(which, formatPercent(Math.abs(percent)), month)
}

/** The change of an amount in pesos. Used for the balance, which can be zero or negative, where a percentage would mislead. */
export function compareAmount(current: number, previous: number, month: string): Comparison {
  const difference = Math.round((current - previous) * 100) / 100
  return build(direction(difference, difference), formatMoneyCompact(Math.abs(difference)), month)
}

/** The three comparisons of the header. Each is null when there is nothing meaningful to compare. */
export interface MonthComparisons {
  income: Comparison | null
  expense: Comparison | null
  balance: Comparison | null
}

export const NO_COMPARISONS: MonthComparisons = { income: null, expense: null, balance: null }

/**
 * Compares the month on screen with the one before. Nothing is compared when there is no previous month, when it had no
 * movements at all (there is nothing to compare against), or when its totals are incomplete for lack of a USD rate
 * (a comparison with a wrong number is worse than none).
 */
export function compareWithPrevious(
  current: { income: number; expense: number; balance: number },
  previous: PreviousMonth | null,
): MonthComparisons {
  if (!previous || previous.hasUnconvertedUsd) return NO_COMPARISONS
  if (previous.income <= 0 && previous.expense <= 0) return NO_COMPARISONS

  const month = monthName(previous.month)
  return {
    income: comparePercent(current.income, previous.income, month),
    expense: comparePercent(current.expense, previous.expense, month),
    balance: compareAmount(current.balance, previous.balance, month),
  }
}
