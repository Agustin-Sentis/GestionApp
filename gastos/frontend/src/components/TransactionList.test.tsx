import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { saveSession } from '../api.ts'
import { downloadBlob } from '../download.ts'
import { mockApi } from '../test/fetchMock.ts'
import { food, pageOf, salary, transaction } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import TransactionList from './TransactionList.tsx'

// jsdom cannot download files: the real helper is replaced, and the tests check what it was handed
vi.mock('../download.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../download.ts')>()),
  downloadBlob: vi.fn(),
}))

const CATEGORY_ID = '3f2b1c9e-7a4d-4e8b-9c1d-2a5b6c7d8e9f'

function signedIn() {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
}

type Reply = Parameters<typeof mockApi>[0][string]

/** Renders the list at a URL, with the given answer for the paged endpoint. Returns the requests it made to it. */
function setup(route: string, paged: Reply, extraRoutes: Record<string, Reply> = {}) {
  signedIn()
  const fetchMock = mockApi({
    'GET /api/category': () => ({ body: [food, salary] }),
    'GET /api/transaction/paged': paged,
    ...extraRoutes,
  })
  renderWithProviders(<TransactionList />, { route })

  return {
    pagedRequests: () =>
      fetchMock.mock.calls
        .map(([url]) => new URL(String(url)))
        .filter((url) => url.pathname === '/api/transaction/paged')
        .map((url) => Object.fromEntries(url.searchParams)),
  }
}

describe('TransactionList rows', () => {
  it('shows each transaction with its date, category, note and a sign by type', async () => {
    setup('/', () => ({
      body: pageOf([
        transaction({ id: 'a', date: '2026-11-05', note: 'almuerzo' }),
        transaction({ id: 'b', date: '2026-11-04', note: null, type: 'Income', categoryName: 'Sueldo', amount: 5000 }),
      ]),
    }))

    expect(await screen.findByText('05/11/2026')).toBeInTheDocument()
    expect(screen.getByText('almuerzo')).toBeInTheDocument()
    expect(screen.getByText(/^−\s?\$/)).toBeInTheDocument() // expense: minus
    expect(screen.getByText(/^\+\s?\$/)).toBeInTheDocument() // income: plus
    expect(screen.getByText('—')).toBeInTheDocument() // no note
  })

  it('shows the saved rate and the peso amount of a USD transaction', async () => {
    setup('/', () => ({
      body: pageOf([transaction({ currency: 'USD', amount: 10, exchangeRate: 1570, arsAmount: 15700 })]),
    }))

    expect(await screen.findByText(/US\$/)).toBeInTheDocument()
    expect(screen.getByText(/≈\s?\$\s?15\.700,00 · cotización\s?\$\s?1\.570,00/)).toBeInTheDocument()
  })

  it('says a USD transaction still has no rate, instead of showing a made-up amount', async () => {
    setup('/', () => ({
      body: pageOf([transaction({ currency: 'USD', amount: 10, exchangeRate: null, arsAmount: null })]),
    }))

    expect(await screen.findByText('Sin cotización todavía')).toBeInTheDocument()
    expect(screen.queryByText(/≈/)).not.toBeInTheDocument()
  })

  it('shows nothing extra for an ARS transaction', async () => {
    setup('/', () => ({ body: pageOf([transaction()]) }))

    await screen.findByText('almuerzo')
    expect(screen.queryByText(/cotización/)).not.toBeInTheDocument()
  })

  it('shows the error when the list cannot be loaded', async () => {
    setup('/', () => ({ status: 500, body: { error: 'Ocurrió un error inesperado.' } }))

    expect(await screen.findByText('Ocurrió un error inesperado.')).toBeInTheDocument()
  })
})

describe('TransactionList asks for what the URL says', () => {
  it('asks for the month of the URL, page 1 and a page size of 20', async () => {
    const { pagedRequests } = setup('/?mes=2026-03', () => ({ body: pageOf([]) }))

    await screen.findByText('No hay movimientos en este mes.')
    expect(pagedRequests()[0]).toEqual({ year: '2026', month: '3', page: '1', pageSize: '20' })
  })

  it('sends the category, type and page of the URL', async () => {
    const { pagedRequests } = setup(
      `/?mes=2026-03&categoria=${CATEGORY_ID}&tipo=Expense&pagina=2`,
      () => ({ body: pageOf([], { page: 2 }) }),
    )

    await waitFor(() => expect(pagedRequests()).toHaveLength(1))
    expect(pagedRequests()[0]).toEqual({
      year: '2026', month: '3', categoryId: CATEGORY_ID, type: 'Expense', page: '2', pageSize: '20',
    })
  })

  it('ignores garbage in the URL instead of sending it to the server', async () => {
    const { pagedRequests } = setup('/?categoria=xyz&tipo=Foo&pagina=-3', () => ({ body: pageOf([]) }))

    await waitFor(() => expect(pagedRequests()).toHaveLength(1))
    const request = pagedRequests()[0]
    expect(request).not.toHaveProperty('categoryId')
    expect(request).not.toHaveProperty('type')
    expect(request.page).toBe('1')
  })
})

describe('TransactionList filters', () => {
  it('offers the categories grouped by type', async () => {
    setup('/', () => ({ body: pageOf([]) }))

    await screen.findByRole('option', { name: 'Comida' })
    expect(screen.getByRole('group', { name: 'Gastos' })).toContainElement(screen.getByRole('option', { name: 'Comida' }))
    expect(screen.getByRole('group', { name: 'Ingresos' })).toContainElement(screen.getByRole('option', { name: 'Sueldo' }))
  })

  it('filters by category and goes back to the first page', async () => {
    const { pagedRequests } = setup('/?pagina=3', () => ({ body: pageOf([], { page: 3 }) }))
    await screen.findByRole('option', { name: 'Comida' })

    await userEvent.selectOptions(screen.getByLabelText('Categoría'), food.id)

    await waitFor(() => expect(pagedRequests().at(-1)).toMatchObject({ categoryId: food.id, page: '1' }))
  })

  it('filters by type', async () => {
    const { pagedRequests } = setup('/', () => ({ body: pageOf([]) }))
    await screen.findByRole('option', { name: 'Comida' })

    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'Income')

    await waitFor(() => expect(pagedRequests().at(-1)).toMatchObject({ type: 'Income' }))
  })

  it('switches to a date range, which replaces the month in the request', async () => {
    const { pagedRequests } = setup('/?mes=2026-03', () => ({ body: pageOf([]) }))
    await screen.findByRole('option', { name: 'Comida' })

    await userEvent.click(screen.getByLabelText('Rango de fechas'))
    fireEvent.change(await screen.findByLabelText('Desde'), { target: { value: '2026-03-05' } })
    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-03-20' } })

    await waitFor(() => expect(pagedRequests().at(-1)).toMatchObject({ from: '2026-03-05', to: '2026-03-20' }))
    expect(pagedRequests().at(-1)).not.toHaveProperty('year')
  })

  it('warns that the summary above still shows the month while a range is used', async () => {
    setup('/?mes=2026-03&rango=1', () => ({ body: pageOf([]) }))

    expect(await screen.findByText(/El resumen de arriba sigue mostrando marzo de 2026/)).toBeInTheDocument()
  })

  it('does not ask the server when the range is backwards, and explains it', async () => {
    const { pagedRequests } = setup('/?rango=1&desde=2026-03-20&hasta=2026-03-05', () => ({ body: pageOf([]) }))

    expect(await screen.findByRole('alert')).toHaveTextContent('«Desde» no puede ser posterior a «Hasta»')
    expect(pagedRequests()).toHaveLength(0)
  })

  it('clears the category, type and range but keeps the month', async () => {
    const { pagedRequests } = setup(`/?mes=2026-03&categoria=${CATEGORY_ID}&tipo=Expense`, () => ({ body: pageOf([]) }))

    await userEvent.click((await screen.findAllByRole('button', { name: 'Limpiar filtros' }))[0])

    await waitFor(() => expect(pagedRequests().at(-1)).toEqual({ year: '2026', month: '3', page: '1', pageSize: '20' }))
  })

  it('only offers to clear filters when there is something to clear', async () => {
    setup('/', () => ({ body: pageOf([transaction()]) }))

    await screen.findByText('almuerzo')
    expect(screen.queryByRole('button', { name: 'Limpiar filtros' })).not.toBeInTheDocument()
  })
})

describe('TransactionList note search', () => {
  it('asks for the text once the user stops typing, from the first page', async () => {
    const { pagedRequests } = setup('/?pagina=3', () => ({ body: pageOf([], { page: 3 }) }))
    await screen.findByRole('option', { name: 'Comida' })

    await userEvent.type(screen.getByLabelText('Buscar en la nota'), 'cena')

    await waitFor(() => expect(pagedRequests().at(-1)).toMatchObject({ search: 'cena', page: '1' }), { timeout: 2000 })
  })

  it('does not ask for every keystroke', async () => {
    const { pagedRequests } = setup('/', () => ({ body: pageOf([]) }))
    await screen.findByRole('option', { name: 'Comida' })

    await userEvent.type(screen.getByLabelText('Buscar en la nota'), 'almuerzo')

    await waitFor(() => expect(pagedRequests().at(-1)).toMatchObject({ search: 'almuerzo' }), { timeout: 2000 })
    const withSearch = pagedRequests().filter((request) => 'search' in request)
    expect(withSearch).toHaveLength(1) // one request for the whole word, not one per letter
  })

  it('starts with the text of the URL, in the box and in the request', async () => {
    const { pagedRequests } = setup('/?buscar=cena', () => ({ body: pageOf([]) }))

    expect(await screen.findByLabelText('Buscar en la nota')).toHaveValue('cena')
    await waitFor(() => expect(pagedRequests()).toHaveLength(1))
    expect(pagedRequests()[0]).toMatchObject({ search: 'cena' })
  })

  it('does not send a search made only of spaces', async () => {
    const { pagedRequests } = setup('/', () => ({ body: pageOf([]) }))
    await screen.findByRole('option', { name: 'Comida' })

    await userEvent.type(screen.getByLabelText('Buscar en la nota'), '   ')
    await new Promise((resolve) => setTimeout(resolve, 500))

    expect(pagedRequests().every((request) => !('search' in request))).toBe(true)
  })

  it('says the filters match nothing when the text finds nothing, and clearing empties the box', async () => {
    const { pagedRequests } = setup('/?buscar=pizza', () => ({ body: pageOf([]) }))
    expect(await screen.findByText('Ningún movimiento coincide con los filtros.')).toBeInTheDocument()

    await userEvent.click(screen.getAllByRole('button', { name: 'Limpiar filtros' })[0])

    await waitFor(() => expect(screen.getByLabelText('Buscar en la nota')).toHaveValue(''))
    await waitFor(() => expect(pagedRequests().at(-1)).not.toHaveProperty('search'))
  })

  it('limits the text to what the server accepts', async () => {
    setup('/', () => ({ body: pageOf([]) }))

    expect(await screen.findByLabelText('Buscar en la nota')).toHaveAttribute('maxlength', '100')
  })
})

describe('TransactionList CSV export', () => {
  const exportRoute = (calls: { url: URL }[]) => (_init: RequestInit, url: URL) => {
    calls.push({ url })
    return { body: 'Fecha;Tipo' }
  }

  it('downloads the file with the filters on screen, but not the page, and says so', async () => {
    const calls: { url: URL }[] = []
    setup('/?mes=2026-03&tipo=Expense&buscar=cena&pagina=2', () => ({
      body: pageOf([transaction()], { total: 45, page: 2, totalPages: 3 }),
    }), { 'GET /api/transaction/export': exportRoute(calls) })

    await userEvent.click(await screen.findByRole('button', { name: 'Exportar CSV' }))

    expect(await screen.findByText('Archivo descargado')).toBeInTheDocument()
    expect(Object.fromEntries(calls[0].url.searchParams)).toEqual({
      year: '2026', month: '3', type: 'Expense', search: 'cena',
    })
    expect(downloadBlob).toHaveBeenCalledWith(expect.any(Blob), 'movimientos-2026-03.csv')
  })

  it('names the file without a month when the list uses a date range', async () => {
    const calls: { url: URL }[] = []
    setup('/?rango=1&desde=2026-03-01&hasta=2026-03-15', () => ({ body: pageOf([transaction()]) }), {
      'GET /api/transaction/export': exportRoute(calls),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Exportar CSV' }))

    await waitFor(() => expect(downloadBlob).toHaveBeenCalledWith(expect.any(Blob), 'movimientos.csv'))
    expect(calls[0].url.searchParams.get('from')).toBe('2026-03-01')
    expect(calls[0].url.searchParams.get('to')).toBe('2026-03-15')
  })

  it('shows the reason when the server refuses the export, and downloads nothing', async () => {
    setup('/', () => ({ body: pageOf([transaction()]) }), {
      'GET /api/transaction/export': () => ({ status: 400, body: { error: 'Hay más de 10000 movimientos para exportar.' } }),
    })
    vi.mocked(downloadBlob).mockClear()

    await userEvent.click(await screen.findByRole('button', { name: 'Exportar CSV' }))

    expect(await screen.findByText('Hay más de 10000 movimientos para exportar.')).toBeInTheDocument()
    expect(downloadBlob).not.toHaveBeenCalled()
  })

  it('cannot be pressed when there is nothing to export', async () => {
    setup('/', () => ({ body: pageOf([]) }))

    await screen.findByText('No hay movimientos en este mes.')
    expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeDisabled()
  })

  it('cannot be pressed while the date range is backwards', async () => {
    setup('/?rango=1&desde=2026-03-20&hasta=2026-03-05', () => ({ body: pageOf([transaction()]) }))

    await screen.findByRole('alert')
    expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeDisabled()
  })
})

describe('TransactionList editing', () => {
  it('has no edit button when nobody listens for it', async () => {
    setup('/', () => ({ body: pageOf([transaction()]) }))

    await screen.findByText('almuerzo')
    expect(screen.queryByRole('button', { name: /^Editar/ })).not.toBeInTheDocument()
  })

  it('hands the row to the editor when its edit button is pressed', async () => {
    signedIn()
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'GET /api/transaction/paged': () => ({ body: pageOf([transaction({ note: 'cena' })]) }),
    })
    const onEdit = vi.fn()
    renderWithProviders(<TransactionList onEdit={onEdit} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Editar movimiento de Comida' }))

    expect(onEdit).toHaveBeenCalledWith(expect.objectContaining({ id: 't1', note: 'cena' }))
  })

  it('marks the row that is being edited', async () => {
    signedIn()
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'GET /api/transaction/paged': () => ({ body: pageOf([transaction({ id: 'a' }), transaction({ id: 'b', note: 'otra' })]) }),
    })
    renderWithProviders(<TransactionList onEdit={vi.fn()} editingId="b" />)

    const marked = (await screen.findByText('otra')).closest('tr')
    expect(marked).toHaveAttribute('aria-current', 'true')
    expect(screen.getByText('almuerzo').closest('tr')).not.toHaveAttribute('aria-current')
  })

  it('tells the editor when the transaction it holds was deleted', async () => {
    signedIn()
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'GET /api/transaction/paged': () => ({ body: pageOf([transaction()]) }),
      'DELETE /api/transaction/t1': () => ({ status: 204 }),
    })
    const onDeleted = vi.fn()
    renderWithProviders(<TransactionList onDeleted={onDeleted} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Eliminar movimiento de Comida' }))
    await userEvent.click(screen.getByRole('button', { name: 'Sí' }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('t1'))
  })
})

describe('TransactionList empty states', () => {
  it('says the month has no transactions when no filter is on', async () => {
    setup('/', () => ({ body: pageOf([]) }))

    expect(await screen.findByText('No hay movimientos en este mes.')).toBeInTheDocument()
  })

  it('says the filters match nothing, and lets the user clear them', async () => {
    setup('/?tipo=Income', () => ({ body: pageOf([]) }))

    expect(await screen.findByText('Ningún movimiento coincide con los filtros.')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Limpiar filtros' }).length).toBeGreaterThan(0)
  })
})

describe('TransactionList pagination', () => {
  const firstOfThree = () => ({ body: pageOf([transaction()], { total: 45, page: 1, totalPages: 3 }) })

  it('shows where the user is and how many movements there are', async () => {
    setup('/', firstOfThree)

    expect(await screen.findByText('Página 1 de 3 · 45 movimientos')).toBeInTheDocument()
  })

  it('cannot go back from the first page', async () => {
    setup('/', firstOfThree)

    expect(await screen.findByRole('button', { name: 'Anterior' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Siguiente' })).toBeEnabled()
  })

  it('cannot go forward from the last page', async () => {
    setup('/?pagina=3', () => ({ body: pageOf([transaction()], { total: 45, page: 3, totalPages: 3 }) }))

    expect(await screen.findByRole('button', { name: 'Siguiente' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeEnabled()
  })

  it('asks for the next page, keeping the filters', async () => {
    const { pagedRequests } = setup('/?tipo=Expense', firstOfThree)

    await userEvent.click(await screen.findByRole('button', { name: 'Siguiente' }))

    await waitFor(() => expect(pagedRequests().at(-1)).toMatchObject({ page: '2', type: 'Expense' }))
  })

  it('asks for the previous page', async () => {
    const { pagedRequests } = setup('/?pagina=3', () => ({
      body: pageOf([transaction()], { total: 45, page: 3, totalPages: 3 }),
    }))

    await userEvent.click(await screen.findByRole('button', { name: 'Anterior' }))

    await waitFor(() => expect(pagedRequests().at(-1)).toMatchObject({ page: '2' }))
  })

  it('shows no page controls when everything fits in one page', async () => {
    setup('/', () => ({ body: pageOf([transaction()]) }))

    expect(await screen.findByText('1 movimiento')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Siguiente' })).not.toBeInTheDocument()
    expect(screen.queryByText(/Página/)).not.toBeInTheDocument()
  })

  it('keeps showing the current rows while the next page loads', async () => {
    let release!: () => void
    const held = new Promise<void>((resolve) => { release = resolve })
    let calls = 0
    setup('/', async () => {
      calls++
      if (calls === 1) return { body: pageOf([transaction({ note: 'primera' })], { total: 45, page: 1, totalPages: 3 }) }
      await held
      return { body: pageOf([transaction({ id: 'z', note: 'segunda' })], { total: 45, page: 2, totalPages: 3 }) }
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Siguiente' }))

    expect(screen.getByText('primera')).toBeInTheDocument() // not replaced by a blank placeholder
    release()
    expect(await screen.findByText('segunda')).toBeInTheDocument()
  })
})

describe('TransactionList after deleting', () => {
  it('steps back a page when the last item of the last page is deleted', async () => {
    let deleted = false
    const { pagedRequests } = setup(
      '/?pagina=2',
      () => ({
        // Page 2 has one item; once it is deleted the server has only one page left and page 2 is past the end
        body: deleted
          ? pageOf([], { total: 20, page: 2, totalPages: 1 })
          : pageOf([transaction()], { total: 21, page: 2, totalPages: 2 }),
      }),
      {
        'DELETE /api/transaction/t1': () => {
          deleted = true
          return { status: 204 }
        },
      },
    )

    await userEvent.click(await screen.findByRole('button', { name: 'Eliminar movimiento de Comida' }))
    await userEvent.click(screen.getByRole('button', { name: 'Sí' }))

    await waitFor(() => expect(pagedRequests().at(-1)).toMatchObject({ page: '1' }))
  })
})
