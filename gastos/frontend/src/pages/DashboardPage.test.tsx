import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import { food, pageOf, transaction } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import DashboardPage from './DashboardPage.tsx'

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
