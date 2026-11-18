import { monthName } from './format.ts'

/** The movements of one day, in the order they came. */
export interface DayGroup<T> {
  /** yyyy-MM-dd */
  date: string
  items: T[]
}

/**
 * Groups neighbors that share a date. The list already comes ordered by date, so a day is always together; this does not
 * sort (that would second-guess the order the server and the user chose), it only joins what is next to each other.
 */
export function groupByDay<T extends { date: string }>(items: T[]): DayGroup<T>[] {
  const groups: DayGroup<T>[] = []
  for (const item of items) {
    const last = groups.at(-1)
    if (last && last.date === item.date) last.items.push(item)
    else groups.push({ date: item.date, items: [item] })
  }
  return groups
}

/** The date as it is written in the margin of the ledger: the big day, the month and, if it is not this year, the year. */
export interface DayMarker {
  day: string
  month: string
  /** Two digits ("25"), only when it is not the current year, because then the month alone would be ambiguous. */
  year: string | null
}

export function dayMarker(date: string, currentYear: number): DayMarker {
  const [year, month, day] = date.split('-').map(Number)
  return {
    day: String(day),
    month: monthName(month).slice(0, 3),
    year: year === currentYear ? null : String(year).slice(2),
  }
}
