import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { mockApi, mockFetchNetworkError } from '../test/fetchMock.ts'
import type { MockReply } from '../test/fetchMock.ts'
import { renderWithProviders } from '../test/render.tsx'
import AuthPage from './AuthPage.tsx'

const NEUTRAL = /Si ese correo tiene una cuenta, te mandamos un enlace para restablecer la contraseña/

/** A fake server for the recovery request. Returns the fake fetch, to see what was sent. */
function serverFor(reply: MockReply = { status: 204 }) {
  return mockApi({ 'POST /api/auth/forgot-password': () => reply })
}

const bodiesOf = (fetchMock: ReturnType<typeof mockApi>) => fetchMock.mock.calls.map(([, init]) => JSON.parse(String(init?.body)))

describe('AuthPage password recovery', () => {
  it('offers the recovery on the sign-in form, and nowhere else', async () => {
    renderWithProviders(<AuthPage />)

    expect(screen.getByRole('button', { name: '¿Olvidaste tu contraseña?' })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: '¿No tenés cuenta? Registrate' }))
    expect(screen.queryByRole('button', { name: '¿Olvidaste tu contraseña?' })).not.toBeInTheDocument()
  })

  it('opens the recovery form: only the email, with its own title and button', async () => {
    renderWithProviders(<AuthPage />)

    await userEvent.click(screen.getByRole('button', { name: '¿Olvidaste tu contraseña?' }))

    expect(screen.getByRole('heading', { name: 'Recuperar contraseña' })).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toBeInTheDocument()
    expect(screen.queryByLabelText('Contraseña')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar enlace' })).toBeInTheDocument()
  })

  it('keeps the email that was already typed', async () => {
    renderWithProviders(<AuthPage />)
    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')

    await userEvent.click(screen.getByRole('button', { name: '¿Olvidaste tu contraseña?' }))

    expect(screen.getByLabelText('Email')).toHaveValue('ana@example.com')
  })

  it('opens on the recovery form when a mail asks for it', () => {
    renderWithProviders(<AuthPage />, { route: '/login?modo=recuperar' })

    expect(screen.getByRole('heading', { name: 'Recuperar contraseña' })).toBeInTheDocument()
  })

  it('sends the email, and answers with the same words whatever the address is', async () => {
    const fetchMock = serverFor()
    renderWithProviders(<AuthPage />, { route: '/login?modo=recuperar' })

    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Enviar enlace' }))

    const status = await screen.findByRole('status')
    expect(status).toHaveTextContent(NEUTRAL)
    expect(status).toHaveTextContent('Vale 1 hora')
    expect(status).toHaveTextContent('carpeta de spam')
    expect(bodiesOf(fetchMock)).toEqual([{ email: 'ana@example.com' }])
    // The form is gone: no way to send twice by accident, and nothing that says whether the account exists
    expect(screen.queryByLabelText('Email')).not.toBeInTheDocument()
  })

  it('says exactly the same for an address nobody registered', async () => {
    serverFor()
    const { unmount } = renderWithProviders(<AuthPage />, { route: '/login?modo=recuperar' })
    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Enviar enlace' }))
    const first = (await screen.findByRole('status')).textContent
    unmount()

    renderWithProviders(<AuthPage />, { route: '/login?modo=recuperar' })
    await userEvent.type(screen.getByLabelText('Email'), 'nadie@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Enviar enlace' }))

    expect((await screen.findByRole('status')).textContent).toBe(first)
  })

  it('goes back to signing in from the answer', async () => {
    serverFor()
    renderWithProviders(<AuthPage />, { route: '/login?modo=recuperar' })
    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Enviar enlace' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Volver a ingresar' }))

    expect(screen.getByText('Ingresá a tu cuenta')).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('goes back to signing in from the form', async () => {
    renderWithProviders(<AuthPage />, { route: '/login?modo=recuperar' })

    await userEvent.click(screen.getByRole('button', { name: 'Volver a ingresar' }))

    expect(screen.getByRole('button', { name: 'Ingresar' })).toBeInTheDocument()
  })

  it('shows the error of too many tries, and lets the user try again', async () => {
    serverFor({ status: 429, body: { error: 'Demasiados intentos. Probá de nuevo en unos minutos.' } })
    renderWithProviders(<AuthPage />, { route: '/login?modo=recuperar' })
    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')

    await userEvent.click(screen.getByRole('button', { name: 'Enviar enlace' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiados intentos.')
    expect(screen.getByRole('button', { name: 'Enviar enlace' })).toBeEnabled()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('shows the error when there is no connection, and keeps the form', async () => {
    mockFetchNetworkError()
    renderWithProviders(<AuthPage />, { route: '/login?modo=recuperar' })
    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')

    await userEvent.click(screen.getByRole('button', { name: 'Enviar enlace' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor')
    expect(screen.getByLabelText('Email')).toHaveValue('ana@example.com')
  })
})

describe('AuthPage', () => {
  it('starts on the sign-in form', () => {
    renderWithProviders(<AuthPage />)

    expect(screen.getByText('Ingresá a tu cuenta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ingresar' })).toBeInTheDocument()
  })

  it('switches to the sign-up form and shows the password rules', async () => {
    renderWithProviders(<AuthPage />)

    await userEvent.click(screen.getByRole('button', { name: '¿No tenés cuenta? Registrate' }))

    expect(screen.getByText('Creá tu cuenta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeInTheDocument()
    expect(screen.getByText(/Mínimo 8 caracteres/)).toBeInTheDocument()
  })

  it('opens on the sign-up form when the landing page asks for a new account', () => {
    renderWithProviders(<AuthPage />, { route: '/login?modo=registro' })

    expect(screen.getByText('Creá tu cuenta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeInTheDocument()
  })

  it('opens on sign-in for any other value of the parameter', () => {
    renderWithProviders(<AuthPage />, { route: '/login?modo=cualquiera' })

    expect(screen.getByText('Ingresá a tu cuenta')).toBeInTheDocument()
  })

  it('has the name of the app as the link back to the landing page', () => {
    renderWithProviders(<AuthPage />)

    expect(screen.getByRole('link', { name: 'DualBalance, volver al inicio' })).toHaveAttribute('href', '/')
  })

  it('leads back to the landing page', () => {
    renderWithProviders(<AuthPage />)

    expect(screen.getByRole('link', { name: '← Volver al inicio' })).toHaveAttribute('href', '/')
  })
})

describe('AuthPage second step (two-step verification)', () => {
  const session = { token: 'jwt', userId: 'u', email: 'ana@example.com', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() }
  const CHALLENGE = { requiresTwoFactor: true, challengeToken: 'challenge-1' }

  /** A server whose sign in asks for the second step; `verify` answers the code. */
  function twoFactorServer(verify: MockReply = { body: session }) {
    return mockApi({
      'POST /api/auth/signin': () => ({ body: CHALLENGE }),
      'POST /api/auth/2fa/verify': () => verify,
    })
  }

  async function signInUntilTheSecondStep() {
    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Demo1234!')
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))
    await screen.findByRole('heading', { name: 'Verificación en dos pasos' })
  }

  it('asks for the code of the app after the password, and forgets the password', async () => {
    twoFactorServer()
    renderWithProviders(<AuthPage />)

    await signInUntilTheSecondStep()

    const input = screen.getByLabelText('Código de 6 dígitos')
    expect(input).toHaveAttribute('inputmode', 'numeric')
    expect(input).toHaveAttribute('autocomplete', 'one-time-code')
    expect(input).toHaveFocus()
    expect(screen.queryByLabelText('Contraseña')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Verificar' })).toBeInTheDocument()
    expect(localStorage.getItem('gastos.session')).toBeNull() // the password alone is not a session
  })

  it('sends the challenge with the code, and starts the session', async () => {
    const fetchMock = twoFactorServer()
    renderWithProviders(<AuthPage />)
    await signInUntilTheSecondStep()

    await userEvent.type(screen.getByLabelText('Código de 6 dígitos'), '123456')
    await userEvent.click(screen.getByRole('button', { name: 'Verificar' }))

    await vi.waitFor(() => expect(JSON.parse(localStorage.getItem('gastos.session') ?? '{}').token).toBe('jwt'))
    const verifyCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/api/auth/2fa/verify'))
    expect(JSON.parse(String(verifyCall?.[1]?.body))).toEqual({ challengeToken: 'challenge-1', code: '123456' })
  })

  it('switches to a recovery code and back', async () => {
    twoFactorServer()
    renderWithProviders(<AuthPage />)
    await signInUntilTheSecondStep()

    await userEvent.click(screen.getByRole('button', { name: 'Usar un código de recuperación' }))
    expect(screen.getByLabelText('Código de recuperación')).toBeInTheDocument()
    expect(screen.queryByLabelText('Código de 6 dígitos')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Usar el código de la app' }))
    expect(screen.getByLabelText('Código de 6 dígitos')).toBeInTheDocument()
  })

  it('goes back to the password, keeping the email and dropping the challenge', async () => {
    const fetchMock = twoFactorServer()
    renderWithProviders(<AuthPage />)
    await signInUntilTheSecondStep()

    await userEvent.click(screen.getByRole('button', { name: 'Volver' }))

    expect(screen.getByLabelText('Email')).toHaveValue('ana@example.com')
    expect(screen.getByLabelText('Contraseña')).toHaveValue('')
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/2fa/verify'))).toBe(false)
  })

  it('says a wrong code and stays on the second step, with the field empty to try again', async () => {
    twoFactorServer({ status: 400, body: { error: 'Código incorrecto o vencido.' } })
    renderWithProviders(<AuthPage />)
    await signInUntilTheSecondStep()

    await userEvent.type(screen.getByLabelText('Código de 6 dígitos'), '000000')
    await userEvent.click(screen.getByRole('button', { name: 'Verificar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Código incorrecto o vencido.')
    expect(screen.getByLabelText('Código de 6 dígitos')).toHaveValue('')
    expect(screen.getByRole('button', { name: 'Verificar' })).toBeDisabled() // blank: nothing to send yet

    await userEvent.type(screen.getByLabelText('Código de 6 dígitos'), '654321')
    expect(screen.getByRole('button', { name: 'Verificar' })).toBeEnabled()
  })

  it('shows the lock with the time to wait', async () => {
    twoFactorServer({ status: 429, body: { error: 'Demasiados intentos. Probá de nuevo en 15 minutos.', retryAfterSeconds: 900 } })
    renderWithProviders(<AuthPage />)
    await signInUntilTheSecondStep()

    await userEvent.type(screen.getByLabelText('Código de 6 dígitos'), '123456')
    await userEvent.click(screen.getByRole('button', { name: 'Verificar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiados intentos. Probá de nuevo en 15 minutos.')
    expect(screen.getByRole('heading', { name: 'Verificación en dos pasos' })).toBeInTheDocument()
  })

  it('goes back to the password, with a notice, when the challenge expired', async () => {
    twoFactorServer({ status: 401, body: { error: 'Tu verificación venció, ingresá de nuevo.' } })
    renderWithProviders(<AuthPage />)
    await signInUntilTheSecondStep()

    await userEvent.type(screen.getByLabelText('Código de 6 dígitos'), '123456')
    await userEvent.click(screen.getByRole('button', { name: 'Verificar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Tu verificación venció, ingresá de nuevo.')
    expect(screen.getByLabelText('Contraseña')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Verificación en dos pasos' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('ana@example.com')
  })

  it('does not show a second step to an account without it', async () => {
    mockApi({ 'POST /api/auth/signin': () => ({ body: { ...session, requiresTwoFactor: false } }) })
    renderWithProviders(<AuthPage />)

    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Demo1234!')
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    await vi.waitFor(() => expect(JSON.parse(localStorage.getItem('gastos.session') ?? '{}').token).toBe('jwt'))
    expect(screen.queryByRole('heading', { name: 'Verificación en dos pasos' })).not.toBeInTheDocument()
  })

  it('does not let a blank code be sent', async () => {
    twoFactorServer()
    renderWithProviders(<AuthPage />)
    await signInUntilTheSecondStep()

    expect(screen.getByRole('button', { name: 'Verificar' })).toBeDisabled()
  })
})
