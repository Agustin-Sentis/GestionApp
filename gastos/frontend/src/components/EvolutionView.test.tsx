import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useLocation } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { saveSession } from '../api.ts'
import type { EvolutionReport, MonthEvolution } from '../api.ts'
import { formatMoney } from '../format.ts'
import { mockApi } from '../test/fetchMock.ts'
import { dateButton, pickMonth } from '../test/datePicker.ts'
import { renderWithProviders } from '../test/render.tsx'
import EvolutionView from './EvolutionView.tsx'

// The chart library draws nothing useful in jsdom (it has no size): what is checked is what the view hands to the chart
vi.mock('./EvolutionChart.tsx', () => ({
  default: ({ points }: { points: unknown[] }) => <div data-testid="chart" data-points={points.length} />,
}))

function month(monthNumber: number, income: number, expense: number, hasUnconvertedUsd = false): MonthEvolution {
  return { year: 2026, month: monthNumber, income, expense, balance: income - expense, hasUnconvertedUsd }
}

function reportOf(overrides: Partial<EvolutionReport> = {}): EvolutionReport {
  const months = overrides.months ?? [month(9, 1000, 400), month(10, 2000, 800)]
  return {
    from: '2026-09',
    to: '2026-10',
    currency: 'ARS',
    months,
    totals: { income: 3000, expense: 1200, balance: 1800 },
    pendingConversions: [],
    hasUnconvertedUsd: false,
    ...overrides,
  }
}

function LocationProbe() {
  return <output data-testid="url">{useLocation().search}</output>
}

function setup(route = '/', report: EvolutionReport | { status: number; error: string } = reportOf(), selectedMonth = '2026-11') {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  const fetchMock = mockApi({
    'GET /api/report/evolution': () => ('error' in report ? { status: report.status, body: { error: report.error } } : { body: report }),
  })
  renderWithProviders(
    <>
      <EvolutionView selectedMonth={selectedMonth} />
      <LocationProbe />
    </>,
    { route },
  )
  return fetchMock
}

/** What was asked of the evolution endpoint, as { from, to }, in order. */
function requests(fetchMock: ReturnType<typeof setup>) {
  return fetchMock.mock.calls
    .map(([url]) => new URL(String(url)))
    .filter((u) => u.pathname === '/api/report/evolution')
    .map((u) => ({ from: u.searchParams.get('from'), to: u.searchParams.get('to') }))
}

describe('EvolutionView', () => {
  it('opens with the last 6 months ending at the month on screen', async () => {
    const fetchMock = setup()

    await screen.findByTestId('chart')
    expect(requests(fetchMock)).toEqual([{ from: '2026-06', to: '2026-11' }])
    expect(screen.getByRole('heading', { name: 'Evolución' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Últimos 6 meses' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('hands the months to the chart and writes them in the table, with the total', async () => {
    setup()

    expect(await screen.findByTestId('chart')).toHaveAttribute('data-points', '2')
    expect(screen.getByRole('row', { name: /^Total/ }).textContent).toContain(formatMoney(3000))
    expect(screen.getByRole('row', { name: /Promedio mensual/ })).toBeInTheDocument()
  })

  it('shows the loading state first', () => {
    setup()

    expect(screen.getByText('Cargando la evolución…')).toBeInTheDocument()
  })

  it('asks for the shortcut written in the address', async () => {
    const fetchMock = setup('/?rango=3')

    await screen.findByTestId('chart')
    expect(requests(fetchMock)).toEqual([{ from: '2026-09', to: '2026-11' }])
    expect(screen.getByRole('button', { name: 'Últimos 3 meses' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('asks for the range written in the address, whatever month is on screen', async () => {
    const fetchMock = setup('/?desde=2026-03&hasta=2026-05')

    await screen.findByTestId('chart')
    expect(requests(fetchMock)).toEqual([{ from: '2026-03', to: '2026-05' }])
    expect(dateButton('Desde')).toHaveTextContent('marzo de 2026')
    expect(dateButton('Hasta')).toHaveTextContent('mayo de 2026')
  })

  it('changes the range with a shortcut, and keeps it in the address', async () => {
    const fetchMock = setup('/?mes=2026-11&desde=2026-03&hasta=2026-05')
    await screen.findByTestId('chart')

    await userEvent.click(screen.getByRole('button', { name: 'Últimos 12 meses' }))

    await waitFor(() => expect(requests(fetchMock).at(-1)).toEqual({ from: '2025-12', to: '2026-11' }))
    const url = new URLSearchParams(screen.getByTestId('url').textContent ?? '')
    expect(url.get('rango')).toBe('12')
    expect(url.has('desde')).toBe(false)
    expect(url.has('hasta')).toBe(false)
    expect(url.get('mes')).toBe('2026-11') // what else was in the address stays
  })

  it('writes the range of the two fields in the address and asks for it', async () => {
    const fetchMock = setup()
    await screen.findByTestId('chart')

    await pickMonth(userEvent.setup(), 'Desde', '2026-01')

    await waitFor(() => expect(requests(fetchMock).at(-1)).toEqual({ from: '2026-01', to: '2026-11' }))
    const url = new URLSearchParams(screen.getByTestId('url').textContent ?? '')
    expect(url.get('desde')).toBe('2026-01')
    expect(url.get('hasta')).toBe('2026-11')
    expect(url.has('rango')).toBe(false)
  })

  it('says why a range cannot be shown, and asks for nothing', async () => {
    const fetchMock = setup('/?desde=2026-09&hasta=2026-03')

    expect(await screen.findByText('El mes inicial no puede ser posterior al final.')).toBeInTheDocument()
    expect(requests(fetchMock)).toEqual([])
    expect(screen.queryByTestId('chart')).not.toBeInTheDocument()
    // The fields keep what was written, so it can be fixed
    expect(dateButton('Desde')).toHaveTextContent('septiembre de 2026')
  })

  it('says a range of more than 24 months cannot be shown', async () => {
    const fetchMock = setup('/?desde=2024-01&hasta=2026-11')

    expect(await screen.findByText('El rango no puede superar los 24 meses.')).toBeInTheDocument()
    expect(requests(fetchMock)).toEqual([])
  })

  it('says there is nothing to show when no month has movements', async () => {
    setup('/', reportOf({ months: [month(9, 0, 0), month(10, 0, 0)], totals: { income: 0, expense: 0, balance: 0 } }))

    expect(await screen.findByText('No hay movimientos en este período.')).toBeInTheDocument()
    expect(screen.queryByTestId('chart')).not.toBeInTheDocument()
  })

  it('shows the error of the server', async () => {
    setup('/', { status: 400, error: 'El rango no puede superar los 24 meses.' })

    expect(await screen.findByText('El rango no puede superar los 24 meses.')).toBeInTheDocument()
    expect(screen.queryByTestId('chart')).not.toBeInTheDocument()
  })

  it('says which dollar was converted with today\'s rate', async () => {
    setup('/', reportOf({ pendingConversions: [{ dollarType: 'Blue', rate: 1400 }] }))

    expect(await screen.findByText(/guardados con el dólar blue no tenían su cotización/)).toBeInTheDocument()
  })

  it('says when a rate could not be obtained, and marks the month that is incomplete', async () => {
    setup(
      '/',
      reportOf({
        months: [month(9, 1000, 400), month(10, 2000, 800, true)],
        pendingConversions: [{ dollarType: 'Official', rate: null }],
        hasUnconvertedUsd: true,
      }),
    )

    expect(await screen.findByText(/No se pudo obtener la cotización oficial/)).toBeInTheDocument()
    expect(screen.getByText('incompleto')).toBeInTheDocument()
  })

  it('does not count the month in progress in the average, and says so', async () => {
    const now = new Date()
    const current = { year: now.getFullYear(), month: now.getMonth() + 1 }
    const currentMonth = `${current.year}-${String(current.month).padStart(2, '0')}`
    setup(
      '/',
      reportOf({
        months: [
          { ...month(1, 1000, 400), year: current.year - 1 },
          { ...month(current.month, 100, 50), year: current.year },
        ],
      }),
      currentMonth,
    )

    expect(await screen.findByText('El promedio no cuenta el mes en curso, que todavía está incompleto.')).toBeInTheDocument()
    expect(screen.getByText('(en curso)')).toBeInTheDocument()
  })
})
