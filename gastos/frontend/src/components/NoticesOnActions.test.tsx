import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import { food, pageOf, transaction } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import CategoryManager from './CategoryManager.tsx'
import TransactionForm from './TransactionForm.tsx'
import TransactionList from './TransactionList.tsx'

const lunch = transaction()

function signedIn() {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
}

async function deleteLunch() {
  await userEvent.click(await screen.findByRole('button', { name: 'Eliminar movimiento de Comida' }))
  await userEvent.click(screen.getByRole('button', { name: 'Sí' }))
}

describe('notices on actions', () => {
  it('confirms a deleted transaction', async () => {
    signedIn()
    const fetchMock = mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'GET /api/transaction/paged': () => ({ body: pageOf([lunch]) }),
      'DELETE /api/transaction/t1': () => ({ status: 204 }),
    })
    renderWithProviders(<TransactionList />)

    await deleteLunch()

    expect(await screen.findByText('Movimiento eliminado')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(true)
  })

  it('shows the reason as a notice when a transaction cannot be deleted', async () => {
    signedIn()
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'GET /api/transaction/paged': () => ({ body: pageOf([lunch]) }),
      'DELETE /api/transaction/t1': () => ({ status: 404, body: { error: 'No se encontró ningún movimiento.' } }),
    })
    renderWithProviders(<TransactionList />)

    await deleteLunch()

    expect(await screen.findByRole('alert')).toHaveTextContent('No se encontró ningún movimiento.')
  })

  it('does not delete anything when the user answers no', async () => {
    signedIn()
    const fetchMock = mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'GET /api/transaction/paged': () => ({ body: pageOf([lunch]) }),
    })
    renderWithProviders(<TransactionList />)

    await userEvent.click(await screen.findByRole('button', { name: 'Eliminar movimiento de Comida' }))
    await userEvent.click(screen.getByRole('button', { name: 'No' }))

    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
    expect(screen.queryByText('Movimiento eliminado')).not.toBeInTheDocument()
  })

  it('confirms a created category and clears the field', async () => {
    signedIn()
    mockApi({
      'GET /api/category': () => ({ body: [] }),
      'POST /api/category': () => ({ status: 201, body: food }),
    })
    renderWithProviders(<CategoryManager />)

    const name = await screen.findByLabelText('Nombre')
    await userEvent.type(name, 'Comida')
    await userEvent.click(screen.getByRole('button', { name: 'Crear' }))

    expect(await screen.findByText('Categoría creada')).toBeInTheDocument()
    expect(name).toHaveValue('')
  })

  it('explains why a category with transactions cannot be deleted', async () => {
    signedIn()
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      [`DELETE /api/category/${food.id}`]: () => ({
        status: 409,
        body: { error: 'La categoría tiene movimientos asociados y no se puede eliminar.' },
      }),
    })
    renderWithProviders(<CategoryManager />)

    await userEvent.click(await screen.findByRole('button', { name: 'Eliminar categoría Comida' }))
    await userEvent.click(screen.getByRole('button', { name: 'Sí' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('La categoría tiene movimientos asociados')
  })

  it('does not delete a category when the user answers no', async () => {
    signedIn()
    const fetchMock = mockApi({ 'GET /api/category': () => ({ body: [food] }) })
    renderWithProviders(<CategoryManager />)

    await userEvent.click(await screen.findByRole('button', { name: 'Eliminar categoría Comida' }))
    await userEvent.click(screen.getByRole('button', { name: 'No' }))

    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
  })

  it('confirms a created transaction', async () => {
    signedIn()
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'POST /api/transaction': () => ({ status: 201, body: lunch }),
    })
    renderWithProviders(<TransactionForm />)

    await userEvent.type(await screen.findByLabelText('Monto'), '1500')
    await userEvent.selectOptions(screen.getByLabelText('Categoría'), food.id)
    await userEvent.click(screen.getByRole('button', { name: 'Agregar' }))

    await waitFor(() => expect(screen.getByText('Movimiento agregado')).toBeInTheDocument())
  })

  it('keeps a form error next to the form instead of in a notice', async () => {
    signedIn()
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'POST /api/transaction': () => ({ status: 400, body: { error: 'El monto debe ser mayor a cero.' } }),
    })
    renderWithProviders(<TransactionForm />)

    await userEvent.type(await screen.findByLabelText('Monto'), '1')
    await userEvent.selectOptions(screen.getByLabelText('Categoría'), food.id)
    await userEvent.click(screen.getByRole('button', { name: 'Agregar' }))

    expect(await screen.findByText('El monto debe ser mayor a cero.')).toBeInTheDocument()
    expect(screen.queryByText('Movimiento agregado')).not.toBeInTheDocument()
  })
})
