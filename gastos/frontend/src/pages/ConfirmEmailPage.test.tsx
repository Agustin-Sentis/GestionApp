import { screen } from '@testing-library/react'
import { StrictMode } from 'react'
import { beforeEach, describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import { mockApi, mockFetchNetworkError } from '../test/fetchMock.ts'
import type { MockReply } from '../test/fetchMock.ts'
import { renderWithProviders } from '../test/render.tsx'
import ConfirmEmailPage from './ConfirmEmailPage.tsx'

/** Opens the page as the link of the mail does, with no session, and returns the fake fetch. */
function open(route: string, reply: MockReply = { status: 204 }) {
  const fetchMock = mockApi({ 'POST /api/settings/confirm-email': () => reply })
  renderWithProviders(<ConfirmEmailPage />, { route })
  return fetchMock
}

const bodies = (fetchMock: ReturnType<typeof mockApi>) => fetchMock.mock.calls.map(([, init]) => JSON.parse(String(init?.body)))

beforeEach(() => {
  localStorage.clear()
  saveSession(null)
})

describe('ConfirmEmailPage', () => {
  it('uses the token of the link, and says the email is confirmed', async () => {
    const fetchMock = open('/confirmar-correo?token=abc_-123')

    expect(await screen.findByText('Correo confirmado.')).toBeInTheDocument()
    expect(bodies(fetchMock)).toEqual([{ token: 'abc_-123' }])
    expect(screen.getByRole('link', { name: /Ir a / })).toHaveAttribute('href', '/')
  })

  it('says it is confirming while it waits', async () => {
    open('/confirmar-correo?token=abc')

    expect(screen.getByText('Confirmando tu correo…')).toBeInTheDocument()
    await screen.findByText('Correo confirmado.')
  })

  it('does not spend the token twice when React runs the page twice (development)', async () => {
    const fetchMock = mockApi({ 'POST /api/settings/confirm-email': () => ({ status: 204 }) })
    renderWithProviders(
      <StrictMode>
        <ConfirmEmailPage />
      </StrictMode>,
      { route: '/confirmar-correo?token=abc' },
    )

    await screen.findByText('Correo confirmado.')
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('says the link expired or was used when the server answers 400, and where to get another', async () => {
    open('/confirmar-correo?token=old', { status: 400, body: { error: 'El enlace venció o ya se usó.' } })

    expect(await screen.findByRole('alert')).toHaveTextContent('El enlace venció o ya se usó.')
    expect(screen.getByText(/pedí que te reenviemos el correo/)).toBeInTheDocument()
    expect(screen.queryByText('Correo confirmado.')).not.toBeInTheDocument()
  })

  it('says to try again later when there is no connection, without blaming the link', async () => {
    mockFetchNetworkError()
    renderWithProviders(<ConfirmEmailPage />, { route: '/confirmar-correo?token=abc' })

    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor')
    expect(screen.getByText('Probá de nuevo en unos minutos.')).toBeInTheDocument()
    expect(screen.queryByText(/venció/)).not.toBeInTheDocument()
  })

  it('says to try again later when there were too many tries (429)', async () => {
    open('/confirmar-correo?token=abc', { status: 429, body: { error: 'Demasiados intentos. Probá de nuevo en unos minutos.' } })

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiados intentos.')
    expect(screen.getByText('Probá de nuevo en unos minutos.')).toBeInTheDocument()
  })

  it.each([['/confirmar-correo'], ['/confirmar-correo?token='], ['/confirmar-correo?token=%20%20']])(
    'asks for the whole link, and sends nothing, when the token is missing (%s)',
    (route) => {
      const fetchMock = open(route)

      expect(screen.getByText(/no trae el código de confirmación/)).toBeInTheDocument()
      expect(fetchMock).not.toHaveBeenCalled()
    },
  )
})
