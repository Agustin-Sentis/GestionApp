import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { saveSession } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import { food, pageOf, salary, transaction } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import DashboardPage from './DashboardPage.tsx'

// The chart library draws nothing useful in jsdom (it has no size): the table next to the chart is what is checked
vi.mock('../components/ReportChart.tsx', () => ({ default: () => <div data-testid="chart" /> }))

const report = {
  year: 2026, month: 11, currency: 'ARS', income: 0, expense: 1500, balance: -1500,
  byCategory: [], usdRateUsed: null, hasUnconvertedUsd: false,
}

function setup(route = '/?mes=2026-11', extraRoutes: Parameters<typeof mockApi>[0] = {}) {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  const fetchMock = mockApi({
    'GET /api/category': () => ({ body: [food] }),
    'GET /api/report/monthly': () => ({ body: report }),
    'GET /api/transaction/paged': () => ({ body: pageOf([transaction()]) }),
    'PUT /api/transaction/t1': () => ({ status: 204 }),
    'DELETE /api/transaction/t1': () => ({ status: 204 }),
    ...extraRoutes,
  })
  renderWithProviders(<DashboardPage />, { route })
  return fetchMock
}

describe('DashboardPage', () => {
  it('follows the month of the URL in the picker, the summary and the list', async () => {
    const fetchMock = setup('/?mes=2026-03')

    await screen.findByText('almuerzo')
    expect(screen.getByLabelText('Mes a visualizar')).toHaveValue('2026-03')
    const urls = fetchMock.mock.calls.map(([url]) => new URL(String(url)))
    expect(urls.find((u) => u.pathname === '/api/report/monthly')?.search).toBe('?year=2026&month=3')
    expect(urls.find((u) => u.pathname === '/api/transaction/paged')?.searchParams.get('month')).toBe('3')
  })

  it('moves to the next and previous month with the arrows, in the summary and the list', async () => {
    const fetchMock = setup('/?mes=2026-12')
    await screen.findByText('almuerzo')

    await userEvent.click(screen.getByRole('button', { name: 'Mes siguiente' }))

    await waitFor(() => expect(screen.getByLabelText('Mes a visualizar')).toHaveValue('2027-01')) // across the year
    await waitFor(() => {
      const paged = fetchMock.mock.calls.map(([url]) => new URL(String(url))).filter((u) => u.pathname === '/api/transaction/paged')
      expect(paged.at(-1)?.searchParams.get('year')).toBe('2027')
      expect(paged.at(-1)?.searchParams.get('month')).toBe('1')
    })

    await userEvent.click(screen.getByRole('button', { name: 'Mes anterior' }))
    await userEvent.click(screen.getByRole('button', { name: 'Mes anterior' }))

    await waitFor(() => expect(screen.getByLabelText('Mes a visualizar')).toHaveValue('2026-11'))
  })

  it('goes back to the first page when the month changes', async () => {
    const fetchMock = setup('/?mes=2026-11&pagina=3', {
      'GET /api/transaction/paged': () => ({ body: pageOf([transaction()], { total: 60, page: 3, totalPages: 3 }) }),
    })
    await screen.findByText('almuerzo')

    await userEvent.click(screen.getByRole('button', { name: 'Mes anterior' }))

    await waitFor(() => {
      const paged = fetchMock.mock.calls.map(([url]) => new URL(String(url))).filter((u) => u.pathname === '/api/transaction/paged')
      expect(paged.at(-1)?.searchParams.get('page')).toBe('1')
    })
  })

  it('cannot go past the months the server accepts', async () => {
    setup('/?mes=2000-01')

    expect(await screen.findByRole('button', { name: 'Mes anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Mes siguiente' })).toBeEnabled()
  })

  it('starts as a form to create a movement', async () => {
    setup()

    expect(await screen.findByRole('heading', { name: 'Nuevo movimiento' })).toBeInTheDocument()
  })

  it('loads a row into the form when "Editar" is pressed, and goes back to creating after saving', async () => {
    const fetchMock = setup()

    await userEvent.click(await screen.findByRole('button', { name: 'Editar movimiento de Comida' }))

    expect(await screen.findByRole('heading', { name: 'Editar movimiento' })).toBeInTheDocument()
    expect(screen.getByLabelText('Monto')).toHaveValue(1500)
    expect(screen.getByText('almuerzo').closest('tr')).toHaveAttribute('aria-current', 'true')

    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(await screen.findByRole('heading', { name: 'Nuevo movimiento' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url, init]) => init?.method === 'PUT' && String(url).endsWith('/api/transaction/t1'))).toBe(true)
    expect(screen.getByLabelText('Monto')).toHaveValue(null) // a clean form, not the edited values
  })

  it('goes back to creating when the edit is cancelled', async () => {
    setup()

    await userEvent.click(await screen.findByRole('button', { name: 'Editar movimiento de Comida' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Cancelar' }))

    expect(await screen.findByRole('heading', { name: 'Nuevo movimiento' })).toBeInTheDocument()
    expect(screen.getByText('almuerzo').closest('tr')).not.toHaveAttribute('aria-current')
  })

  it('goes back to creating when the transaction being edited is deleted', async () => {
    setup()

    await userEvent.click(await screen.findByRole('button', { name: 'Editar movimiento de Comida' }))
    await screen.findByRole('heading', { name: 'Editar movimiento' })
    await userEvent.click(screen.getByRole('button', { name: 'Eliminar movimiento de Comida' }))
    await userEvent.click(screen.getByRole('button', { name: 'Sí' }))

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Nuevo movimiento' })).toBeInTheDocument())
  })

  it('updates the summary chart to the real percentages when an income is added', async () => {
    let incomeAdded = false
    setup('/?mes=2026-11', {
      'GET /api/category': () => ({ body: [food, salary] }),
      'GET /api/report/monthly': () => ({
        body: incomeAdded
          ? { ...report, income: 100000, expense: 1500, balance: 98500, byCategory: [
              { name: 'Sueldo', type: 'Income', total: 100000 }, { name: 'Comida', type: 'Expense', total: 1500 }] }
          : { ...report, income: 0, expense: 1500, balance: -1500, byCategory: [{ name: 'Comida', type: 'Expense', total: 1500 }] },
      }),
      'POST /api/transaction': () => {
        incomeAdded = true
        return { status: 201, body: transaction({ type: 'Income', categoryId: salary.id, categoryName: 'Sueldo', amount: 100000 }) }
      },
    })

    // Before: no income yet, so the chart is the expenses by category and Comida is all of it
    expect(await screen.findByRole('columnheader', { name: '% de los gastos' })).toBeInTheDocument()
    // "Comida" is also the category of a row in the list of movements: look only in the chart's table
    const comidaRow = () =>
      within(screen.getByRole('table', { name: 'Detalle del gráfico' })).getByRole('cell', { name: /^Comida/ }).closest('tr')!
    expect(within(comidaRow()).getByText('100%')).toBeInTheDocument()

    const form = screen.getByRole('heading', { name: 'Nuevo movimiento' }).closest('form')!
    await within(form).findByRole('option', { name: 'Sueldo' })
    await userEvent.type(within(form).getByLabelText('Monto'), '100000')
    await userEvent.selectOptions(within(form).getByLabelText('Categoría'), salary.id)
    await userEvent.click(within(form).getByRole('button', { name: 'Agregar' }))

    // After: the summary asked again, and the chart now shows what share of the income each expense took
    expect(await screen.findByRole('columnheader', { name: '% de los ingresos' })).toBeInTheDocument()
    expect(within(comidaRow()).getByText('1,5%')).toBeInTheDocument()
    expect(screen.getByText('Sin gastar')).toBeInTheDocument()
  })

  it('has three tabs on a narrow screen, one per part, and the matching panels', async () => {
    setup()

    const tabs = await screen.findAllByRole('tab')

    expect(tabs.map((tab) => tab.textContent)).toEqual(['Resumen', 'Libro', 'Anotar'])
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true')
    for (const tab of tabs) {
      const panel = document.getElementById(tab.getAttribute('aria-controls')!)
      expect(panel).toHaveAttribute('role', 'tabpanel')
      expect(panel).toHaveAttribute('aria-labelledby', tab.id)
    }
  })

  it('changes tab, and takes the user to the form when a row is edited', async () => {
    setup()
    await screen.findByText('almuerzo')

    await userEvent.click(screen.getByRole('tab', { name: 'Libro' }))
    expect(screen.getByRole('tab', { name: 'Libro' })).toHaveAttribute('aria-selected', 'true')

    await userEvent.click(screen.getByRole('button', { name: 'Editar movimiento de Comida' }))
    expect(screen.getByRole('tab', { name: 'Anotar' })).toHaveAttribute('aria-selected', 'true')
  })

  it('cannot draw the tab labels over each other, or push the header past the screen, on a very narrow one', async () => {
    setup()

    for (const tab of await screen.findAllByRole('tab')) {
      // Each label may shrink and, as a last resort, be cut with "…" instead of overlapping its neighbor
      expect(tab.className).toContain('min-w-0')
      expect(tab.className).toContain('truncate')
    }
    // The controls of the header wrap, and the month field can shrink
    expect(screen.getByRole('group', { name: 'Tema' }).parentElement?.className).toContain('flex-wrap')
    expect(screen.getByLabelText('Mes a visualizar').className).toContain('min-w-0')
  })

  it('switches the form to another transaction when a different row is edited', async () => {
    setup('/?mes=2026-11', {
      'GET /api/transaction/paged': () => ({
        body: pageOf([transaction({ id: 't1', amount: 1500 }), transaction({ id: 't2', amount: 800, note: 'cena' })]),
      }),
    })

    const [first, second] = await screen.findAllByRole('button', { name: 'Editar movimiento de Comida' })
    await userEvent.click(first)
    await waitFor(() => expect(screen.getByLabelText('Monto')).toHaveValue(1500))
    await userEvent.click(second)

    await waitFor(() => expect(screen.getByLabelText('Monto')).toHaveValue(800))
    expect(screen.getByLabelText('Nota (opcional)')).toHaveValue('cena')
  })
})
