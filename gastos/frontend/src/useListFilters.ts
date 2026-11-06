import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { TransactionQuery, TransactionType } from './api.ts'
import { currentMonthValue, parseMonthValue } from './format.ts'

/** Items per page. Fixed: the list has no control to change it. */
export const PAGE_SIZE = 20

/** The longest text the server accepts when searching the notes. */
export const SEARCH_MAX_LENGTH = 100

/** What the list shows, as the user chose it. Always valid: anything wrong in the URL has already been replaced. */
export interface ListFilters {
  /** yyyy-MM. The summary always follows it; the list follows it unless the date range is on. */
  month: string
  categoryId: string | null
  type: TransactionType | null
  /** The list uses From/To instead of the month. */
  rangeMode: boolean
  /** yyyy-MM-dd, or '' when not set. Only used in range mode. Either end may stay empty. */
  from: string
  to: string
  /** Text to look for inside the notes, or '' for no search. Never has spaces around it. */
  search: string
  page: number
}

// The URL is what the user (or a pasted link, or an old bookmark) typed: none of it can be trusted.
const MONTH = /^(\d{4})-(0[1-9]|1[0-2])$/
const DAY = /^\d{4}-\d{2}-\d{2}$/
const PAGE_NUMBER = /^[1-9]\d{0,8}$/ // 1 to 999,999,999
const GUID =/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function validMonth(value: string | null, fallback: string): string {
  const match = value ? MONTH.exec(value) : null
  if (!match) return fallback
  const year = Number(match[1])
  return year >= 2000 && year <= 2100 ? value! : fallback
}

/** A real calendar day: "2026-02-31" has the right shape but does not exist. */
function validDay(value: string | null): string {
  if (!value || !DAY.test(value)) return ''
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  const real = date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
  return real ? value : ''
}

/** Turns the URL's query string into valid filters. Pure, so it is easy to test. */
export function parseFilters(params: URLSearchParams, currentMonth: string): ListFilters {
  const category = params.get('categoria')
  const type = params.get('tipo')
  // Digits only, no leading zero, and small enough for the server's 32-bit integer: Number() alone would accept "1e3" or "0x10"
  const pageText = params.get('pagina') ?? ''
  const page = PAGE_NUMBER.test(pageText) ? Number(pageText) : 1

  return {
    month: validMonth(params.get('mes'), currentMonth),
    categoryId: category && GUID.test(category) ? category : null,
    type: type === 'Expense' || type === 'Income' ? type : null,
    rangeMode: params.get('rango') === '1',
    from: validDay(params.get('desde')),
    to: validDay(params.get('hasta')),
    search: (params.get('buscar') ?? '').trim().slice(0, SEARCH_MAX_LENGTH).trim(),
    page,
  }
}

/** True when both ends are set and the start is after the end: the server would answer 400, so nobody asks. */
export function isRangeInverted(filters: ListFilters): boolean {
  return filters.rangeMode && filters.from !== '' && filters.to !== '' && filters.from > filters.to
}

/** The request for the paged list: the month, or the range, plus category, type and page. */
export function toQuery(filters: ListFilters): TransactionQuery {
  const common = {
    categoryId: filters.categoryId ?? undefined,
    type: filters.type ?? undefined,
    search: filters.search || undefined,
    page: filters.page,
    pageSize: PAGE_SIZE,
  }
  if (filters.rangeMode) {
    return { ...common, from: filters.from || undefined, to: filters.to || undefined }
  }
  const { year, month } = parseMonthValue(filters.month)
  return { ...common, year, month }
}

/**
 * The list's filters and page live in the URL (?mes=2026-11&categoria=…&tipo=Expense&pagina=2), so reloading or going back
 * keeps the view, and a link reproduces it. Values equal to the default are left out to keep the URL short.
 * Changing any filter goes back to page 1: page 4 of the old filter means nothing under the new one.
 */
export function useListFilters() {
  const [params, setParams] = useSearchParams()
  const queryString = params.toString()

  const filters = useMemo(
    () => parseFilters(new URLSearchParams(queryString), currentMonthValue()),
    [queryString],
  )

  /** Applies changes to the current URL. A null/empty value removes the parameter. */
  const update = useCallback(
    (changes: Record<string, string | null>, { keepPage = false, replace = false } = {}) => {
      setParams(
        (previous) => {
          const next = new URLSearchParams(previous)
          for (const [key, value] of Object.entries(changes)) {
            if (value === null || value === '') next.delete(key)
            else next.set(key, value)
          }
          if (!keepPage) next.delete('pagina')
          return next
        },
        { replace },
      )
    },
    [setParams],
  )

  return {
    filters,
    query: useMemo(() => toQuery(filters), [filters]),
    rangeInverted: isRangeInverted(filters),
    /** True when anything narrows the list beyond the month (a category, a type, a text or the range). */
    hasExtraFilters: filters.categoryId !== null || filters.type !== null || filters.search !== '' || filters.rangeMode,

    setMonth: (month: string) => update({ mes: month === currentMonthValue() ? null : month }),
    setCategory: (categoryId: string | null) => update({ categoria: categoryId }),
    setType: (type: TransactionType | null) => update({ tipo: type }),
    // replace: typing must not fill the browser history with one entry per pause
    setSearch: (text: string) => update({ buscar: text.trim() }, { replace: true }),
    setRangeMode: (on: boolean) => update({ rango: on ? '1' : null, desde: null, hasta: null }),
    setFrom: (day: string) => update({ desde: day }, { replace: true }),
    setTo: (day: string) => update({ hasta: day }, { replace: true }),
    setPage: (page: number) => update({ pagina: page > 1 ? String(page) : null }, { keepPage: true }),
    /** Back to "the whole month": drops category, type, text, range and page, and keeps the month. */
    clear: () => update({ categoria: null, tipo: null, buscar: null, rango: null, desde: null, hasta: null }),
  }
}
