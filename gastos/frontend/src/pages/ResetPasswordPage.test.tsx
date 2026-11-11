import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { loadSession, saveSession } from '../api.ts'
import { mockApi, mockFetchNetworkError } from '../test/fetchMock.ts'
import type { MockReply } from '../test/fetchMock.ts'
import { renderWithProviders } from '../test/render.tsx'
import ResetPasswordPage from './ResetPasswordPage.tsx'

const GOOD = 'Nueva1234!'

// The rules hint lives inside the label, so the name of the field starts with this and goes on
const NEW_PASSWORD = /^Contraseña nueva/

/** Opens the page as the link of the mail does. `replies` are the answers of the server, one per attempt (the last one repeats). */
function open(route: string, ...replies: MockReply[]) {
  let attempt = 0
  const fetchMock = mockApi({
    'POST /api/auth/reset-password': () => replies[Math.min(attempt++, replies.length - 1)] ?? { status: 204 },
  })
  renderWithProviders(<ResetPasswordPage />, { route })
  return fetchMock
}

const bodiesOf = (fetchMock: ReturnType<typeof mockApi>) => fetchMock.mock.calls.map(([, init]) => JSON.parse(String(init?.body)))

async function fill(password: string, repeat = password) {
  await userEvent.type(screen.getByLabelText(NEW_PASSWORD), password)
  await userEvent.type(screen.getByLabelText('Repetir la contraseña'), repeat)
}

const submit = () => userEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }))

beforeEach(() => {
  localStorage.clear()
  saveSession(null)
})

describe('ResetPasswordPage', () => {
  it('asks the new password twice, and tells the rules', () => {
    open('/restablecer-contrasena?token=abc')

    expect(screen.getByRole('heading', { name: 'Nueva contraseña' })).toBeInTheDocument()
    expect(screen.getByLabelText(NEW_PASSWORD)).toHaveAttribute('autocomplete', 'new-password')
    expect(screen.getByLabelText('Repetir la contraseña')).toHaveAttribute('autocomplete', 'new-password')
    expect(screen.getByText(/Mínimo 8 caracteres/)).toBeInTheDocument()
  })

  it('sends the token of the link with the new password, and says it worked', async () => {
    const fetchMock = open('/restablecer-contrasena?token=abc_-123')

    await fill(GOOD)
    await submit()

    expect(await screen.findByText('Contraseña cambiada.')).toBeInTheDocument()
    expect(bodiesOf(fetchMock)).toEqual([{ token: 'abc_-123', newPassword: GOOD }])
    expect(screen.getByRole('link', { name: 'Ir a ingresar' })).toHaveAttribute('href', '/login')
    expect(screen.queryByLabelText(NEW_PASSWORD)).not.toBeInTheDocument()
  })

  it('closes the session this browser had, because the server stops accepting it', async () => {
    saveSession({ token: 'old-jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
    open('/restablecer-contrasena?token=abc')
    expect(loadSession()).not.toBeNull()

    await fill(GOOD)
    await submit()

    await screen.findByText('Contraseña cambiada.')
    expect(loadSession()).toBeNull()
  })

  it('does not send anything when the two passwords differ', async () => {
    const fetchMock = open('/restablecer-contrasena?token=abc')

    await fill(GOOD, 'Otra12345!')
    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('Las contraseñas no coinciden.')
    expect(fetchMock).not.toHaveBeenCalled()
    // The form stays, to fix it
    expect(screen.getByLabelText(NEW_PASSWORD)).toBeInTheDocument()
  })

  it('says the link expired or was used, hides the form, and offers a new link', async () => {
    open('/restablecer-contrasena?token=old', { status: 400, body: { error: 'El enlace venció o ya se usó.' } })

    await fill(GOOD)
    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('El enlace venció o ya se usó.')
    expect(screen.queryByLabelText(NEW_PASSWORD)).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Pedir uno nuevo' })).toHaveAttribute('href', '/login?modo=recuperar')
  })

  it('shows what the server says about a password that breaks the rules, keeps the form, and lets the user try again', async () => {
    const fetchMock = open(
      '/restablecer-contrasena?token=abc',
      { status: 400, body: { error: 'La contraseña debe contener al menos un carácter especial.' } },
      { status: 204 },
    )

    await fill('Nueva12345')
    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('al menos un carácter especial')
    // The link is still good: no "ask for a new one", and the form is still there
    expect(screen.queryByRole('link', { name: 'Pedir uno nuevo' })).not.toBeInTheDocument()
    await userEvent.clear(screen.getByLabelText(NEW_PASSWORD))
    await userEvent.clear(screen.getByLabelText('Repetir la contraseña'))
    await fill(GOOD)
    await submit()

    expect(await screen.findByText('Contraseña cambiada.')).toBeInTheDocument()
    expect(bodiesOf(fetchMock).map((b) => b.newPassword)).toEqual(['Nueva12345', GOOD])
  })

  it.each([['/restablecer-contrasena'], ['/restablecer-contrasena?token='], ['/restablecer-contrasena?token=%20%20']])(
    'asks for the whole link, shows no form, and sends nothing, when the token is missing (%s)',
    (route) => {
      const fetchMock = open(route)

      expect(screen.getByRole('alert')).toHaveTextContent('no trae el código para restablecer la contraseña')
      expect(screen.queryByLabelText(NEW_PASSWORD)).not.toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'Pedir uno nuevo' })).toHaveAttribute('href', '/login?modo=recuperar')
      expect(fetchMock).not.toHaveBeenCalled()
    },
  )

  it('shows the error of too many tries, and keeps the form', async () => {
    open('/restablecer-contrasena?token=abc', { status: 429, body: { error: 'Demasiados intentos. Probá de nuevo en unos minutos.' } })

    await fill(GOOD)
    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiados intentos.')
    expect(screen.getByLabelText(NEW_PASSWORD)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cambiar contraseña' })).toBeEnabled()
  })

  it('shows the error when there is no connection, without blaming the link', async () => {
    mockFetchNetworkError()
    renderWithProviders(<ResetPasswordPage />, { route: '/restablecer-contrasena?token=abc' })

    await fill(GOOD)
    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor')
    expect(screen.queryByRole('link', { name: 'Pedir uno nuevo' })).not.toBeInTheDocument()
    expect(screen.getByLabelText(NEW_PASSWORD)).toBeInTheDocument()
  })
})
