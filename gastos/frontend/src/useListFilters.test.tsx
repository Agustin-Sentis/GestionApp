import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter, useLocation, useNavigationType } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { PAGE_SIZE, SEARCH_MAX_LENGTH, isRangeInverted, parseFilters, toQuery, useListFilters } from './useListFilters.ts'

const NOVEMBER = '2026-11'
const CATEGORY = '3f2b1c9e-7a4d-4e8b-9c1d-2a5b6c7d8e9f'
const parse = (search: string) => parseFilters(new URLSearchParams(search), NOVEMBER)

describe('parseFilters', () => {
  it('uses the defaults when the URL says nothing', () => {
    expect(parse('')).toEqual({
      month: NOVEMBER, categoryId: null, type: null, rangeMode: false, from: '', to: '', search: '', page: 1,
    })
  })

  it('reads every filter from the URL', () => {
    const filters = parse(
      `mes=2026-03&categoria=${CATEGORY}&tipo=Income&buscar=cena&rango=1&desde=2026-03-01&hasta=2026-03-15&pagina=4`,
    )

    expect(filters).toEqual({
      month: '2026-03', categoryId: CATEGORY, type: 'Income', rangeMode: true, from: '2026-03-01', to: '2026-03-15',
      search: 'cena', page: 4,
    })
  })

  it('trims the search text and cuts it at the length the server accepts', () => {
    expect(parse('buscar=%20%20almuerzo%20%20').search).toBe('almuerzo')
    expect(parse('buscar=%20%20').search).toBe('')
    expect(parse(`buscar=${'a'.repeat(150)}`).search).toBe('a'.repeat(SEARCH_MAX_LENGTH))
  })

  it('keeps special characters in the search text as they are', () => {
    expect(parse(`buscar=${encodeURIComponent('50% off & más_')}`).search).toBe('50% off & más_')
  })

  it.each(['2026-13', '2026-00', '1999-12', '2101-01', '2026-3', 'noviembre', '2026-11-05', ''])(
    'falls back to the current month for the invalid month "%s"',
    (month) => {
      expect(parse(`mes=${month}`).month).toBe(NOVEMBER)
    },
  )

  it.each(['xyz', '123', `${CATEGORY}0`, "'; drop table"])('ignores the invalid category "%s"', (category) => {
    expect(parse(`categoria=${category}`).categoryId).toBeNull()
  })

  it.each(['expense', 'EXPENSE', 'Gasto', 'Foo', ''])('ignores the invalid type "%s"', (type) => {
    expect(parse(`tipo=${type}`).type).toBeNull()
  })

  it.each(['0', '-1', '2.5', 'abc', '', '1e3', '0x10', '007', ' 5', '1000000000', '999999999999999999999'])(
    'goes to page 1 for the invalid page "%s"',
    (page) => {
      expect(parse(`pagina=${page}`).page).toBe(1)
    },
  )

  it('accepts ordinary page numbers, up to nine digits', () => {
    expect(parse('pagina=1').page).toBe(1)
    expect(parse('pagina=25').page).toBe(25)
    expect(parse('pagina=999999999').page).toBe(999999999)
  })

  it('rejects days that have the right shape but do not exist', () => {
    expect(parse('desde=2026-02-31&hasta=2026-13-01').from).toBe('')
    expect(parse('desde=2026-02-31&hasta=2026-13-01').to).toBe('')
  })

  it('accepts a leap day only in a leap year', () => {
    expect(parse('desde=2028-02-29').from).toBe('2028-02-29')
    expect(parse('desde=2026-02-29').from).toBe('')
  })

  it('is in range mode only when asked, not just because dates are present', () => {
    expect(parse('desde=2026-11-01').rangeMode).toBe(false)
    expect(parse('rango=1').rangeMode).toBe(true)
    expect(parse('rango=true').rangeMode).toBe(false)
  })
})

describe('toQuery', () => {
  it('asks for the month, with the category, type, page and a fixed page size', () => {
    const query = toQuery(parse(`mes=2026-03&categoria=${CATEGORY}&tipo=Expense&pagina=2`))

    expect(query).toEqual({ year: 2026, month: 3, categoryId: CATEGORY, type: 'Expense', page: 2, pageSize: PAGE_SIZE })
  })

  it('asks for the range instead of the month in range mode', () => {
    const query = toQuery(parse('mes=2026-03&rango=1&desde=2026-03-05&hasta=2026-03-20'))

    expect(query).toEqual({ from: '2026-03-05', to: '2026-03-20', page: 1, pageSize: PAGE_SIZE })
    expect(query).not.toHaveProperty('year')
  })

  it('sends the search text, and leaves it out when there is none', () => {
    expect(toQuery(parse('buscar=cena')).search).toBe('cena')
    expect(toQuery(parse('')).search).toBeUndefined()
  })

  it('sends the search text in range mode too', () => {
    expect(toQuery(parse('rango=1&desde=2026-03-05&buscar=cena')).search).toBe('cena')
  })

  it('leaves out the end of the range that is not set', () => {
    const query = toQuery(parse('rango=1&desde=2026-03-05'))

    expect(query.from).toBe('2026-03-05')
    expect(query.to).toBeUndefined()
  })
})

describe('isRangeInverted', () => {
  it('is true only in range mode with both ends set and the start after the end', () => {
    expect(isRangeInverted(parse('rango=1&desde=2026-03-20&hasta=2026-03-05'))).toBe(true)
    expect(isRangeInverted(parse('rango=1&desde=2026-03-05&hasta=2026-03-20'))).toBe(false)
    expect(isRangeInverted(parse('rango=1&desde=2026-03-05&hasta=2026-03-05'))).toBe(false)
    expect(isRangeInverted(parse('rango=1&desde=2026-03-20'))).toBe(false)
    expect(isRangeInverted(parse('desde=2026-03-20&hasta=2026-03-05'))).toBe(false) // not in range mode
  })
})

function renderFilters(initial = '/') {
  const wrapper = ({ children }: { children: ReactNode }) => <MemoryRouter initialEntries={[initial]}>{children}</MemoryRouter>
  return renderHook(() => ({ list: useListFilters(), location: useLocation() }), { wrapper })
}

describe('useListFilters', () => {
  it('starts from the URL it was opened with', () => {
    const { result } = renderFilters(`/?mes=2026-03&tipo=Expense&pagina=3`)

    expect(result.current.list.filters).toMatchObject({ month: '2026-03', type: 'Expense', page: 3 })
  })

  it('goes back to page 1 when a filter changes', () => {
    const { result } = renderFilters('/?pagina=4')

    act(() => result.current.list.setType('Income'))

    expect(result.current.list.filters).toMatchObject({ type: 'Income', page: 1 })
    expect(result.current.location.search).toBe('?tipo=Income')
  })

  it('keeps the filters when only the page changes', () => {
    const { result } = renderFilters(`/?categoria=${CATEGORY}`)

    act(() => result.current.list.setPage(3))

    expect(result.current.list.filters).toMatchObject({ categoryId: CATEGORY, page: 3 })
  })

  it('does not write the first page or the current month in the URL', () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 10, 5))
    const { result } = renderFilters('/?mes=2026-03&pagina=2')

    act(() => result.current.list.setMonth('2026-11')) // the current month: it is the default
    act(() => result.current.list.setPage(1))

    expect(result.current.location.search).toBe('')
  })

  it('writes another month and resets the page', () => {
    const { result } = renderFilters('/?pagina=2')

    act(() => result.current.list.setMonth('2026-03'))

    expect(result.current.location.search).toBe('?mes=2026-03')
  })

  it('removes a filter when it is set back to nothing', () => {
    const { result } = renderFilters(`/?categoria=${CATEGORY}`)

    act(() => result.current.list.setCategory(null))

    expect(result.current.location.search).toBe('')
    expect(result.current.list.filters.categoryId).toBeNull()
  })

  it('turns the range on and off, dropping the dates when it goes off', () => {
    const { result } = renderFilters('/')

    act(() => result.current.list.setRangeMode(true))
    act(() => result.current.list.setFrom('2026-03-01'))
    act(() => result.current.list.setTo('2026-03-31'))
    expect(result.current.list.filters).toMatchObject({ rangeMode: true, from: '2026-03-01', to: '2026-03-31' })

    act(() => result.current.list.setRangeMode(false))
    expect(result.current.list.filters).toMatchObject({ rangeMode: false, from: '', to: '' })
    expect(result.current.location.search).toBe('')
  })

  it('writes the search text, trimmed, and goes back to page 1', () => {
    const { result } = renderFilters('/?pagina=3')

    act(() => result.current.list.setSearch('  cena  '))

    expect(result.current.location.search).toBe('?buscar=cena')
    expect(result.current.list.filters).toMatchObject({ search: 'cena', page: 1 })
  })

  it('removes the search from the URL when the text is emptied', () => {
    const { result } = renderFilters('/?buscar=cena')

    act(() => result.current.list.setSearch('   '))

    expect(result.current.location.search).toBe('')
    expect(result.current.list.filters.search).toBe('')
  })

  it('replaces the history entry when the search changes, instead of adding one per pause', () => {
    const wrapper = ({ children }: { children: ReactNode }) => <MemoryRouter initialEntries={['/']}>{children}</MemoryRouter>
    const { result } = renderHook(() => ({ list: useListFilters(), navigationType: useNavigationType() }), { wrapper })

    act(() => result.current.list.setSearch('ce'))

    expect(result.current.navigationType).toBe('REPLACE')
  })

  it('clear drops category, type, search, range and page, but keeps the month', () => {
    const { result } = renderFilters(
      `/?mes=2026-03&categoria=${CATEGORY}&tipo=Expense&buscar=cena&rango=1&desde=2026-03-01&pagina=2`,
    )

    act(() => result.current.list.clear())

    expect(result.current.location.search).toBe('?mes=2026-03')
    expect(result.current.list.hasExtraFilters).toBe(false)
  })

  it('says when something narrows the list beyond the month', () => {
    expect(renderFilters('/').result.current.list.hasExtraFilters).toBe(false)
    expect(renderFilters('/?tipo=Income').result.current.list.hasExtraFilters).toBe(true)
    expect(renderFilters('/?rango=1').result.current.list.hasExtraFilters).toBe(true)
    expect(renderFilters('/?buscar=cena').result.current.list.hasExtraFilters).toBe(true)
    expect(renderFilters('/?buscar=%20').result.current.list.hasExtraFilters).toBe(false)
  })

  it('survives a hostile URL without throwing', () => {
    const { result } = renderFilters('/?mes=%00&categoria=%3Cscript%3E&tipo[]=x&pagina=999999999999999999999')

    expect(result.current.list.filters.month).toMatch(/^\d{4}-\d{2}$/)
    expect(result.current.list.filters.categoryId).toBeNull()
    expect(Number.isInteger(result.current.list.filters.page)).toBe(true)
  })
})
