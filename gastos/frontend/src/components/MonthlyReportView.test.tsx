import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useLocation } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { saveSession } from '../api.ts'
import type { MonthlyReport } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import { food, salary } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import MonthlyReportView from './MonthlyReportView.tsx'

// The chart library draws nothing useful in jsdom (it has no size): what is checked is what the view hands to the chart
vi.mock('./ReportChart.tsx', () => ({
  default: ({ view }: { view: { shape: string; slices: unknown[] } }) => (
    <div data-testid="chart" data-shape={view.shape} data-slices={view.slices.length} />
  ),
}))

function reportOf(overrides: Partial<MonthlyReport> = {}): MonthlyReport {
  return {
    year: 2026, month: 11, currency: 'ARS', income: 100000, expense: 70000, balance: 30000,
    byCategory: [
      { name: 'Sueldo', type: 'Income', total: 100000 },
      { name: 'Alquiler', type: 'Expense', total: 40000 },
      { name: 'Comida', type: 'Expense', total: 25000 },
      { name: 'Transporte', type: 'Expense', total: 5000 },
    ],
    usdRateUsed: null,
    hasUnconvertedUsd: false,
    ...overrides,
  }
}

function LocationProbe() {
  return <output data-testid="url">{useLocation().search}</output>
}

function setup(report: MonthlyReport, route = '/') {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  mockApi({
    'GET /api/category': () => ({
      body: [
        { id: 'a', name: 'Alquiler', type: 'Expense' },
        { ...food },
        { id: 't', name: 'Transporte', type: 'Expense' },
        { ...salary },
      ],
    }),
    'GET /api/report/monthly': () => ({ body: report }),
  })
  renderWithProviders(
    <>
      <MonthlyReportView year={2026} month={11} />
      <LocationProbe />
    </>,
    { route },
  )
}

/** The rows of the detail table as [name, amount, percent] text. */
function tableRows() {
  return screen.getAllByRole('row').slice(1).map((row) => within(row).getAllByRole('cell').map((cell) => cell.textContent))
}

describe('MonthlyReportView charts', () => {
  it('opens with where the income went when the month has income', async () => {
    setup(reportOf())

    expect(await screen.findByLabelText('Gráfico')).toHaveValue('destino')
    expect(screen.getByRole('columnheader', { name: '% de los ingresos' })).toBeInTheDocument()
    expect(tableRows().map(([name, , percent]) => [name, percent])).toEqual([
      ['Alquiler', '40%'],
      ['Comida', '25%'],
      ['Transporte', '5%'],
      ['Sin gastar', '30%'],
    ])
    expect(screen.getByText('Gastaste el 70% de lo que ingresó este mes.')).toBeInTheDocument()
    expect(await screen.findByTestId('chart')).toHaveAttribute('data-shape', 'donut')
  })

  it('opens with the expenses by category when there is no income', async () => {
    setup(reportOf({ income: 0, balance: -70000, byCategory: [{ name: 'Comida', type: 'Expense', total: 70000 }] }))

    expect(await screen.findByLabelText('Gráfico')).toHaveValue('gastos')
    expect(screen.getByRole('columnheader', { name: '% de los gastos' })).toBeInTheDocument()
    expect(tableRows()).toHaveLength(1)
  })

  it('offers every chart', async () => {
    setup(reportOf())

    const options = within(await screen.findByLabelText('Gráfico')).getAllByRole('option').map((o) => o.textContent)

    expect(options).toEqual([
      'Destino de los ingresos',
      'Gastos por categoría',
      'Ingresos por categoría',
      'Ranking de categorías',
      'Ingresos vs. gastos',
    ])
  })

  it('switches the chart and the table, and remembers the choice in the URL', async () => {
    setup(reportOf(), '/?mes=2026-11&pagina=2')
    await userEvent.selectOptions(await screen.findByLabelText('Gráfico'), 'gastos')

    expect(screen.getByRole('columnheader', { name: '% de los gastos' })).toBeInTheDocument()
    expect(tableRows().map(([name]) => name)).toEqual(['Alquiler', 'Comida', 'Transporte'])
    // The other parameters of the page are not touched
    expect(screen.getByTestId('url')).toHaveTextContent('?mes=2026-11&pagina=2&grafico=gastos')
  })

  it('shows the chart the URL asks for', async () => {
    setup(reportOf(), '/?grafico=ranking')

    expect(await screen.findByLabelText('Gráfico')).toHaveValue('ranking')
    expect(await screen.findByTestId('chart')).toHaveAttribute('data-shape', 'bars')
    // The name and, in muted type, whether it is an income or an expense
    expect(tableRows().map(([name]) => name)).toEqual(['SueldoIngreso', 'AlquilerGasto', 'ComidaGasto', 'TransporteGasto'])
  })

  it('compares the income and the expenses in two bars', async () => {
    setup(reportOf(), '/?grafico=comparacion')

    expect(await screen.findByTestId('chart')).toHaveAttribute('data-slices', '2')
    expect(tableRows().map(([name, , percent]) => [name, percent])).toEqual([['Ingresos', '100%'], ['Gastos', '70%']])
  })

  it('ignores a chart name that does not exist and uses the default one', async () => {
    setup(reportOf(), '/?grafico=torta')

    expect(await screen.findByLabelText('Gráfico')).toHaveValue('destino')
  })

  it('says why there is no chart instead of drawing an empty one', async () => {
    setup(reportOf({ income: 0, balance: -5, byCategory: [{ name: 'Comida', type: 'Expense', total: 5 }] }), '/?grafico=ingresos')

    expect(await screen.findByText('No hay ingresos cargados en este mes.')).toBeInTheDocument()
    expect(screen.queryByTestId('chart')).not.toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('asks for an income when the chart of the income is open and there is none', async () => {
    setup(reportOf({ income: 0, balance: -5, byCategory: [{ name: 'Comida', type: 'Expense', total: 5 }] }), '/?grafico=destino')

    expect(await screen.findByText(/cargá uno/)).toBeInTheDocument()
  })

  it('gives a category the same color in the table of every chart', async () => {
    setup(reportOf())
    await screen.findByLabelText('Gráfico')
    // getByText finds the span that holds the name; its first child span is the colored square
    const swatchOf = (name: string) => screen.getByText(name).querySelector('span')?.getAttribute('style')

    const inDestino = swatchOf('Comida')
    await userEvent.selectOptions(screen.getByLabelText('Gráfico'), 'gastos')

    expect(inDestino).toContain('var(--series-2)') // second category of the user's list
    expect(swatchOf('Comida')).toBe(inDestino)
  })

  it('still shows the figures when the categories could not be loaded', async () => {
    saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
    mockApi({
      'GET /api/category': () => ({ status: 500, body: { error: 'caído' } }),
      'GET /api/report/monthly': () => ({ body: reportOf() }),
    })
    renderWithProviders(<MonthlyReportView year={2026} month={11} />)

    expect(await screen.findByText('Alquiler')).toBeInTheDocument()
    expect(screen.getByText('Sin gastar')).toBeInTheDocument()
  })
})

describe('MonthlyReportView figures', () => {
  it('leaves the headline figures of the month to the header: this panel only has the charts', async () => {
    setup(reportOf())

    await screen.findByLabelText('Gráfico')
    expect(screen.queryByText('Te queda')).not.toBeInTheDocument()
  })

  it('says which rate converted the USD movements', async () => {
    setup(reportOf({ usdRateUsed: 1570 }))

    expect(await screen.findByText(/se convirtieron a pesos con la cotización/)).toBeInTheDocument()
  })
})
