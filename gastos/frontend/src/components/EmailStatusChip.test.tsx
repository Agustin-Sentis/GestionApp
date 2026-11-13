import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import type { Settings } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import type { MockReply } from '../test/fetchMock.ts'
import { settings } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import EmailStatusChip from './EmailStatusChip.tsx'

interface Setup {
  initial?: Settings
  getReply?: MockReply
  resendReply?: MockReply
  /** What the server answers to the email change instead of saving it. */
  changeReply?: MockReply
}

/** Renders the chip against a fake server that remembers the address, and returns what the server was asked. */
function setup({ initial = settings({ emailConfirmed: false }), getReply, resendReply, changeReply }: Setup = {}) {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  let saved = initial
  const resends: number[] = []
  const changes: unknown[] = []
  mockApi({
    'GET /api/settings': () => getReply ?? { body: saved },
    'POST /api/settings/resend-confirmation': () => {
      resends.push(1)
      return resendReply ?? { status: 204 }
    },
    'PUT /api/settings/email': (init) => {
      changes.push(JSON.parse(String(init.body)))
      if (changeReply) return changeReply
      saved = { ...saved, email: (changes.at(-1) as { email: string }).email, emailConfirmed: false }
      return { body: saved }
    },
  })
  renderWithProviders(<EmailStatusChip />)
  return { resends, changes }
}

const chip = () => screen.findByRole('button', { name: /Correo sin confirmar/ })

beforeEach(() => localStorage.clear())

describe('EmailStatusChip', () => {
  it('shows a chip written in words while the address is not confirmed', async () => {
    setup()

    expect(await chip()).toHaveAttribute('aria-expanded', 'false')
  })

  it('shows nothing when the address is confirmed', async () => {
    setup({ initial: settings({ emailConfirmed: true }) })

    await waitFor(() => expect(screen.queryByRole('button', { name: /Correo sin confirmar/ })).not.toBeInTheDocument())
  })

  it('shows nothing when the settings could not be loaded', async () => {
    setup({ getReply: { status: 500, body: { error: 'caído' } } })

    // Give the failed request the time to arrive: without the saved state there is nothing to say
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('opens a popover with the address and the two ways out', async () => {
    const user = userEvent.setup()
    setup()

    await user.click(await chip())

    expect(screen.getByRole('button', { name: /Correo sin confirmar/ })).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText('ana@example.com')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reenviar el correo' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '¿Te equivocaste de correo?' })).toBeInTheDocument()
  })

  it('closes with Escape, and gives the focus back to the chip', async () => {
    const user = userEvent.setup()
    setup()
    await user.click(await chip())

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('button', { name: 'Reenviar el correo' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Correo sin confirmar/ })).toHaveFocus()
  })

  it('closes when the click is outside, and stays open when it is inside', async () => {
    const user = userEvent.setup()
    setup()
    await user.click(await chip())

    await user.click(screen.getByText('ana@example.com'))
    expect(screen.getByRole('button', { name: 'Reenviar el correo' })).toBeInTheDocument()

    await user.click(document.body)
    expect(screen.queryByRole('button', { name: 'Reenviar el correo' })).not.toBeInTheDocument()
  })

  it('resends the mail and says so', async () => {
    const user = userEvent.setup()
    const { resends } = setup()
    await user.click(await chip())

    await user.click(screen.getByRole('button', { name: 'Reenviar el correo' }))

    expect(await screen.findByText('Te mandamos otro correo a ana@example.com')).toBeInTheDocument()
    expect(resends).toHaveLength(1)
  })

  it('shows what the server says when the resend is asked too soon', async () => {
    const user = userEvent.setup()
    setup({ resendReply: { status: 400, body: { error: 'Esperá un minuto antes de pedir otro correo.' } } })
    await user.click(await chip())

    await user.click(screen.getByRole('button', { name: 'Reenviar el correo' }))

    expect(await screen.findByText('Esperá un minuto antes de pedir otro correo.')).toBeInTheDocument()
  })

  it('shows what the server says when the mail could not be sent (503)', async () => {
    const user = userEvent.setup()
    setup({ resendReply: { status: 503, body: { error: 'No se pudo enviar el correo.' } } })
    await user.click(await chip())

    await user.click(screen.getByRole('button', { name: 'Reenviar el correo' }))

    expect(await screen.findByText('No se pudo enviar el correo.')).toBeInTheDocument()
  })

  async function openTheForm(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await chip())
    await user.click(screen.getByRole('button', { name: '¿Te equivocaste de correo?' }))
  }

  it('changes the address with the password, shows the new one and goes back to the first view', async () => {
    const user = userEvent.setup()
    const { changes } = setup()
    await openTheForm(user)

    await user.type(screen.getByLabelText('Correo nuevo'), 'nuevo@example.com')
    await user.type(screen.getByLabelText('Tu contraseña'), 'Demo1234!')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Te mandamos un correo a nuevo@example.com')).toBeInTheDocument()
    expect(changes).toEqual([{ email: 'nuevo@example.com', password: 'Demo1234!' }])
    // The first view again, with the new address
    expect(await screen.findByRole('button', { name: 'Reenviar el correo' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Tu contraseña')).not.toBeInTheDocument()
    expect(screen.getAllByText('nuevo@example.com').length).toBeGreaterThan(0)
  })

  it('shows the error of a wrong password inside the form, and keeps what was typed', async () => {
    const user = userEvent.setup()
    setup({ changeReply: { status: 400, body: { error: 'La contraseña no es correcta.' } } })
    await openTheForm(user)

    await user.type(screen.getByLabelText('Correo nuevo'), 'nuevo@example.com')
    await user.type(screen.getByLabelText('Tu contraseña'), 'mala')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    const form = screen.getByRole('form', { name: 'Cambiar el correo' })
    expect(await within(form).findByRole('alert')).toHaveTextContent('La contraseña no es correcta.')
    expect(screen.getByLabelText('Correo nuevo')).toHaveValue('nuevo@example.com')
  })

  it('shows the error when the address is taken (409)', async () => {
    const user = userEvent.setup()
    setup({ changeReply: { status: 409, body: { error: 'Ya existe un usuario con ese email.' } } })
    await openTheForm(user)

    await user.type(screen.getByLabelText('Correo nuevo'), 'otra@example.com')
    await user.type(screen.getByLabelText('Tu contraseña'), 'Demo1234!')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Ya existe un usuario con ese email.')
  })

  it('does not ask the server while a field is empty', async () => {
    const user = userEvent.setup()
    const { changes } = setup()
    await openTheForm(user)

    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(changes).toHaveLength(0)
    expect(await screen.findByRole('alert')).toHaveTextContent('Escribí el correo nuevo y tu contraseña.')
  })

  it('cancel goes back to the first view without asking anything, and forgets the password', async () => {
    const user = userEvent.setup()
    const { changes } = setup()
    await openTheForm(user)
    await user.type(screen.getByLabelText('Tu contraseña'), 'Demo1234!')

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(changes).toHaveLength(0)
    expect(screen.getByRole('button', { name: 'Reenviar el correo' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: '¿Te equivocaste de correo?' }))
    expect(screen.getByLabelText('Tu contraseña')).toHaveValue('')
  })
})
