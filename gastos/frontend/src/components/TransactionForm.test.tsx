import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { saveSession } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import { food, salary, transaction } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import { todayLocal } from '../format.ts'
import { useListFilters } from '../useListFilters.ts'
import TransactionForm from './TransactionForm.tsx'

function signedIn() {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
}

/** The body of the last request made with this method. */
function lastBody(fetchMock: ReturnType<typeof mockApi>, method: string) {
  const call = fetchMock.mock.calls.filter(([, init]) => init?.method === method).at(-1)
  return call ? JSON.parse(String(call[1]?.body)) : undefined
}

function setup(props: Parameters<typeof TransactionForm>[0] = {}, routes: Parameters<typeof mockApi>[0] = {}, route = '/?mes=2026-11') {
  signedIn()
  const fetchMock = mockApi({
    'GET /api/category': () => ({ body: [food, salary] }),
    'POST /api/transaction': () => ({ status: 201, body: transaction() }),
    [`PUT /api/transaction/${transaction().id}`]: () => ({ status: 204 }),
    ...routes,
  })
  renderWithProviders(<TransactionForm {...props} />, { route })
  return fetchMock
}

describe('TransactionForm creating', () => {
  it('is a "new movement" form with no cancel button', async () => {
    setup()

    expect(await screen.findByRole('heading', { name: 'Nuevo movimiento' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Agregar' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cancelar' })).not.toBeInTheDocument()
  })

  it('sends a new transaction with the amount as a number and an empty note as null', async () => {
    const fetchMock = setup()

    await userEvent.type(await screen.findByLabelText('Monto'), '1500.5')
    await userEvent.selectOptions(screen.getByLabelText('Categoría'), food.id)
    await userEvent.click(screen.getByRole('button', { name: 'Agregar' }))

    await waitFor(() => expect(lastBody(fetchMock, 'POST')).toMatchObject({
      amount: 1500.5, currency: 'ARS', categoryId: food.id, note: null,
    }))
  })

  it('asks to create a category first when there are none', async () => {
    setup({}, { 'GET /api/category': () => ({ body: [] }) })

    expect(await screen.findByText('Primero creá al menos una categoría (más abajo).')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Agregar' })).toBeDisabled()
  })
})

/** Shows the month the page is looking at, and lets a test change it the way the arrows above the list do. */
function MonthControls() {
  const { filters, setMonth } = useListFilters()
  return (
    <>
      <output aria-label="mes visto">{filters.month}</output>
      <button onClick={() => setMonth('2000-04')}>ir a abril de 2000</button>
    </>
  )
}

function setupWithControls(route: string) {
  signedIn()
  const fetchMock = mockApi({
    'GET /api/category': () => ({ body: [food] }),
    'POST /api/transaction': () => ({ status: 201, body: transaction() }),
    [`PUT /api/transaction/${transaction().id}`]: () => ({ status: 204 }),
  })
  renderWithProviders(
    <>
      <MonthControls />
      <TransactionForm />
    </>,
    { route },
  )
  return fetchMock
}

describe('TransactionForm and the month on screen', () => {
  it('starts the date inside a past month, on its last day', async () => {
    setupWithControls('/?mes=2000-03')

    expect(await screen.findByLabelText('Fecha')).toHaveValue('2000-03-31')
  })

  it('starts the date inside a future month, on its first day', async () => {
    setupWithControls('/?mes=2099-01')

    expect(await screen.findByLabelText('Fecha')).toHaveValue('2099-01-01')
  })

  it('starts the date today when the month on screen is the current one', async () => {
    setupWithControls('/')

    expect(await screen.findByLabelText('Fecha')).toHaveValue(todayLocal())
  })

  it('moves the suggested date along when the month on screen changes', async () => {
    setupWithControls('/?mes=2000-03')
    await screen.findByLabelText('Fecha')

    await userEvent.click(screen.getByText('ir a abril de 2000'))

    await waitFor(() => expect(screen.getByLabelText('Fecha')).toHaveValue('2000-04-30'))
  })

  it('does not overwrite a date the user chose when the month changes', async () => {
    setupWithControls('/?mes=2000-03')
    fireEvent.change(await screen.findByLabelText('Fecha'), { target: { value: '2000-03-10' } })

    await userEvent.click(screen.getByText('ir a abril de 2000'))

    expect(screen.getByLabelText('Fecha')).toHaveValue('2000-03-10')
  })

  it('does not add a month to the notice when the movement lands in the month on screen', async () => {
    setupWithControls('/?mes=2000-03')
    await userEvent.type(await screen.findByLabelText('Monto'), '100')
    await userEvent.selectOptions(screen.getByLabelText('Categoría'), food.id)

    await userEvent.click(screen.getByRole('button', { name: 'Agregar' }))

    expect(await screen.findByText('Movimiento agregado')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ver' })).not.toBeInTheDocument()
  })

  it('says where the movement went when its date is in another month, and offers to go there', async () => {
    setupWithControls('/?mes=2000-03')
    await userEvent.type(await screen.findByLabelText('Monto'), '100')
    await userEvent.selectOptions(screen.getByLabelText('Categoría'), food.id)
    fireEvent.change(screen.getByLabelText('Fecha'), { target: { value: '2026-11-05' } })

    await userEvent.click(screen.getByRole('button', { name: 'Agregar' }))

    expect(await screen.findByText('Movimiento agregado en noviembre de 2026')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Ver' }))

    expect(screen.getByLabelText('mes visto')).toHaveTextContent('2026-11')
    expect(screen.queryByRole('button', { name: 'Ver' })).not.toBeInTheDocument() // the notice closed itself
  })

  it('also says it when an edit moves a movement to another month', async () => {
    signedIn()
    const editing = transaction({ date: '2000-03-10' })
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      [`PUT /api/transaction/${editing.id}`]: () => ({ status: 204 }),
    })
    renderWithProviders(<TransactionForm editing={editing} />, { route: '/?mes=2000-03' })

    fireEvent.change(await screen.findByLabelText('Fecha'), { target: { value: '2000-05-02' } })
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(await screen.findByText('Movimiento actualizado en mayo de 2000')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ver' })).toBeInTheDocument()
  })

  it('keeps the date of the movement being edited when the month changes', async () => {
    signedIn()
    mockApi({ 'GET /api/category': () => ({ body: [food] }) })
    renderWithProviders(
      <>
        <MonthControls />
        <TransactionForm editing={transaction({ date: '2000-03-10' })} />
      </>,
      { route: '/?mes=2000-03' },
    )

    await userEvent.click(await screen.findByText('ir a abril de 2000'))

    expect(screen.getByLabelText('Fecha')).toHaveValue('2000-03-10')
  })
})

describe('TransactionForm editing', () => {
  const editing = transaction({ amount: 1500, note: 'almuerzo', date: '2026-11-05' })

  it('loads the values of the transaction being edited', async () => {
    setup({ editing })

    expect(await screen.findByRole('heading', { name: 'Editar movimiento' })).toBeInTheDocument()
    expect(screen.getByLabelText('Monto')).toHaveValue(1500)
    expect(screen.getByLabelText('Moneda')).toHaveValue('ARS')
    expect(screen.getByLabelText('Fecha')).toHaveValue('2026-11-05')
    expect(screen.getByLabelText('Nota (opcional)')).toHaveValue('almuerzo')
    await waitFor(() => expect(screen.getByLabelText('Categoría')).toHaveValue(food.id))
  })

  it('puts the cursor in the amount, so the edit can start typing right away', async () => {
    setup({ editing })

    await waitFor(() => expect(screen.getByLabelText('Monto')).toHaveFocus())
  })

  it('saves with PUT to the transaction, shows a notice and lets go of the edit', async () => {
    const onFinishEditing = vi.fn()
    const fetchMock = setup({ editing, onFinishEditing })
    const amount = await screen.findByLabelText('Monto')

    await userEvent.clear(amount)
    await userEvent.type(amount, '2000')
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(await screen.findByText('Movimiento actualizado')).toBeInTheDocument()
    expect(onFinishEditing).toHaveBeenCalledTimes(1)
    expect(lastBody(fetchMock, 'PUT')).toEqual({
      amount: 2000, currency: 'ARS', date: '2026-11-05', note: 'almuerzo', categoryId: food.id,
    })
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false) // it edited, it did not create
  })

  it('can move the transaction to a category of the other type', async () => {
    const fetchMock = setup({ editing })
    await waitFor(() => expect(screen.getByLabelText('Categoría')).toHaveValue(food.id))

    await userEvent.selectOptions(screen.getByLabelText('Categoría'), salary.id)
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    await waitFor(() => expect(lastBody(fetchMock, 'PUT')).toMatchObject({ categoryId: salary.id }))
  })

  it('cancels without sending anything', async () => {
    const onFinishEditing = vi.fn()
    const fetchMock = setup({ editing, onFinishEditing })

    await userEvent.click(await screen.findByRole('button', { name: 'Cancelar' }))

    expect(onFinishEditing).toHaveBeenCalledTimes(1)
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'PUT' || init?.method === 'POST')).toBe(false)
  })

  it('cancels with Escape', async () => {
    const onFinishEditing = vi.fn()
    setup({ editing, onFinishEditing })
    await screen.findByRole('heading', { name: 'Editar movimiento' })

    fireEvent.keyDown(screen.getByLabelText('Monto'), { key: 'Escape' })

    expect(onFinishEditing).toHaveBeenCalledTimes(1)
  })

  it('does not treat Escape as cancel when it is only creating', async () => {
    const onFinishEditing = vi.fn()
    setup({ onFinishEditing })
    await screen.findByRole('heading', { name: 'Nuevo movimiento' })

    fireEvent.keyDown(screen.getByLabelText('Monto'), { key: 'Escape' })

    expect(onFinishEditing).not.toHaveBeenCalled()
  })

  it('keeps the edit open and shows the reason when the server refuses it', async () => {
    const onFinishEditing = vi.fn()
    setup({ editing, onFinishEditing }, {
      [`PUT /api/transaction/${editing.id}`]: () => ({ status: 400, body: { error: 'El monto debe ser mayor a cero.' } }),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Guardar cambios' }))

    expect(await screen.findByText('El monto debe ser mayor a cero.')).toBeInTheDocument()
    expect(onFinishEditing).not.toHaveBeenCalled()
    expect(screen.getByRole('heading', { name: 'Editar movimiento' })).toBeInTheDocument()
  })

  it('says which dollar a USD transaction was saved with, and what happens to it if the amount or the date change', async () => {
    setup(
      { editing: transaction({ currency: 'USD', exchangeRate: 1555, arsAmount: 15550, dollarType: 'Blue' }) },
      { 'GET /api/settings': () => ({ body: { dollarType: 'Mep' } }) },
    )

    const note = await screen.findByText(/Este movimiento se guardó con la cotización blue del día en que se cargó/)
    // It is saved again with the dollar chosen now (MEP), not with the one it had: that is what "saving again" means
    await waitFor(() => expect(note).toHaveTextContent('se guarda de nuevo con la MEP, la de hoy'))
  })

  it('names the dollar the transaction keeps, not the one the user has chosen now', async () => {
    setup(
      { editing: transaction({ currency: 'USD', exchangeRate: 1570, arsAmount: 15700, dollarType: 'Official' }) },
      { 'GET /api/settings': () => ({ body: { dollarType: 'Blue' } }) },
    )

    expect(await screen.findByText(/Este movimiento se guardó con la cotización oficial/)).toBeInTheDocument()
  })

  it('still explains it before the chosen dollar has arrived, without naming one it does not know', async () => {
    setup(
      { editing: transaction({ currency: 'USD', exchangeRate: 1570, arsAmount: 15700 }) },
      { 'GET /api/settings': () => ({ status: 500, body: { error: 'caído' } }) },
    )

    const note = await screen.findByText(/Este movimiento se guardó con la cotización oficial/)
    expect(note).toHaveTextContent('se guarda de nuevo con la que tengas elegida, la de hoy')
  })

  it('tells a new USD movement which rate it will get, and that it stays that way', async () => {
    setup({}, { 'GET /api/settings': () => ({ body: { dollarType: 'Blue' } }) })

    await userEvent.selectOptions(await screen.findByLabelText('Moneda'), 'USD')

    const note = await screen.findByText(/Se va a guardar con la cotización blue de hoy/)
    expect(note).toHaveTextContent('queda así aunque después elijas otro dólar')
  })

  it.each([
    ['Official', 'oficial'],
    ['Blue', 'blue'],
    ['Mep', 'MEP'],
  ] as const)('names the %s dollar in that note', async (dollarType, name) => {
    setup({}, { 'GET /api/settings': () => ({ body: { dollarType } }) })

    await userEvent.selectOptions(await screen.findByLabelText('Moneda'), 'USD')

    expect(await screen.findByText(new RegExp(`Se va a guardar con la cotización ${name} de hoy`))).toBeInTheDocument()
  })

  it('does not mention any rate for a movement in pesos, new or being edited', async () => {
    setup({ editing })

    await screen.findByRole('heading', { name: 'Editar movimiento' })
    expect(screen.queryByText(/cotización/)).not.toBeInTheDocument()
  })

  it('stops mentioning the rate when the currency goes back to pesos', async () => {
    setup()

    await userEvent.selectOptions(await screen.findByLabelText('Moneda'), 'USD')
    expect(await screen.findByText(/Se va a guardar con la cotización/)).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText('Moneda'), 'ARS')

    expect(screen.queryByText(/Se va a guardar con la cotización/)).not.toBeInTheDocument()
  })
})
