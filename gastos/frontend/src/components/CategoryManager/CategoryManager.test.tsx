import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { saveSession } from '../../Services/api/api.ts'
import { mockApi } from '../../test/fetchMock.ts'
import { food } from '../../test/fixtures.ts'
import { renderWithProviders } from '../../test/render.tsx'
import CategoryManager from './CategoryManager.tsx'

/** Renders the manager and returns the bodies of the POSTs it sent. */
function setup() {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  const created: unknown[] = []
  mockApi({
    'GET /api/category': () => ({ body: [food] }),
    'POST /api/category': (init) => {
      created.push(JSON.parse(String(init.body)))
      return { status: 201, body: { ...food, id: 'new', name: 'Gimnasio' } }
    },
  })
  renderWithProviders(<CategoryManager />)
  return created
}

describe('CategoryManager', () => {
  it('lists the categories', async () => {
    setup()

    expect(await screen.findByText('Comida')).toBeInTheDocument()
  })

  it('creates a category with the name trimmed', async () => {
    const created = setup()
    await screen.findByText('Comida')

    await userEvent.type(screen.getByLabelText('Nombre'), '  Gimnasio  ')
    await userEvent.click(screen.getByRole('button', { name: 'Crear' }))

    await waitFor(() => expect(created).toHaveLength(1))
    expect(created[0]).toMatchObject({ name: 'Gimnasio', type: 'Expense' })
  })
})

describe('CategoryManager validations', () => {
  it('does not create a category without a name, and says so under the field', async () => {
    const created = setup()
    await screen.findByText('Comida')

    await userEvent.click(screen.getByRole('button', { name: 'Crear' }))

    expect(await screen.findByText('El nombre de la categoría es obligatorio.')).toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByText('Hay algunos campos incorrectos, revisalos.')).toBeInTheDocument()
    expect(created).toEqual([])
  })

  it('takes a name of only spaces as no name', async () => {
    const created = setup()
    await screen.findByText('Comida')

    await userEvent.type(screen.getByLabelText('Nombre'), '    ')
    await userEvent.click(screen.getByRole('button', { name: 'Crear' }))

    expect(await screen.findByText('El nombre de la categoría es obligatorio.')).toBeInTheDocument()
    expect(created).toEqual([])
  })

  it('does not create a name over 60 characters (a pasted one gets past the limit of the field)', async () => {
    const created = setup()
    await screen.findByText('Comida')
    fireEvent.change(screen.getByLabelText('Nombre'), { target: { value: 'x'.repeat(61) } })

    await userEvent.click(screen.getByRole('button', { name: 'Crear' }))

    expect(await screen.findByText('El nombre de la categoría no puede superar los 60 caracteres.')).toBeInTheDocument()
    expect(created).toEqual([])
  })

  it('the error goes away when the person types', async () => {
    setup()
    await screen.findByText('Comida')
    await userEvent.click(screen.getByRole('button', { name: 'Crear' }))
    expect(await screen.findByText('El nombre de la categoría es obligatorio.')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Nombre'), 'G')

    expect(screen.queryByText('El nombre de la categoría es obligatorio.')).not.toBeInTheDocument()
  })
})
