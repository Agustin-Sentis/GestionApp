import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import type { Settings } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import type { MockReply } from '../test/fetchMock.ts'
import { settings } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import BudgetAlertsToggle from './BudgetAlertsToggle.tsx'

interface Setup {
  initial?: Settings
  /** What the server answers to the PUT instead of saving the switch. */
  putReply?: MockReply
  getReply?: MockReply
}

/** Renders the switch against a fake server that remembers the switch, and returns the bodies of the PUTs. */
function setup({ initial = settings(), putReply, getReply }: Setup = {}) {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  let saved = initial
  const puts: unknown[] = []
  mockApi({
    'GET /api/settings': () => getReply ?? { body: saved },
    'PUT /api/settings/budget-alerts': (init) => {
      const { enabled } = JSON.parse(String(init.body)) as { enabled: boolean }
      puts.push({ enabled })
      if (putReply) return putReply
      // Like the server: with an unconfirmed address, turning it on leaves it waiting for the link
      saved = { ...saved, budgetEmailAlerts: enabled }
      return { body: saved }
    },
  })
  renderWithProviders(<BudgetAlertsToggle />)
  return puts
}

/** The text under the switch. The toasts are role="status" too, so it is found through the description of the checkbox. */
const status = () => document.getElementById(screen.getByRole('checkbox').getAttribute('aria-describedby')!)!

const checkbox = () => screen.findByRole('checkbox', { name: /Avisarme por correo cuando un presupuesto llegue al 80% o lo supere/ })

beforeEach(() => localStorage.clear())

describe('BudgetAlertsToggle', () => {
  it('starts off, and says nothing is sent', async () => {
    setup()

    expect(await checkbox()).not.toBeChecked()
    expect(status()).toHaveTextContent('Apagado. No te mandamos ningún correo.')
  })

  it('shows the unconfirmed state: on, waiting for the link, with the address and a way to resend', async () => {
    setup({ initial: settings({ budgetEmailAlerts: true, emailConfirmed: false }) })

    expect(await checkbox()).toBeChecked()
    expect(status()).toHaveTextContent('Te mandamos un correo a ana@example.com para confirmar')
    expect(status()).toHaveTextContent('Los avisos empiezan cuando hagas clic en el enlace')
    expect(screen.getByRole('button', { name: 'Reenviar el correo' })).toBeInTheDocument()
  })

  it('shows the confirmed state: on, with the address, and nothing to resend', async () => {
    setup({ initial: settings({ budgetEmailAlerts: true, emailConfirmed: true }) })

    expect(await checkbox()).toBeChecked()
    expect(status()).toHaveTextContent('Activados. Te avisamos a ana@example.com.')
    expect(screen.queryByRole('button', { name: 'Reenviar el correo' })).not.toBeInTheDocument()
  })

  it('turning it on asks the server, says the confirmation mail was sent, and shows the waiting state', async () => {
    const user = userEvent.setup()
    const puts = setup()

    await user.click(await checkbox())

    expect(await screen.findByText('Te mandamos un correo para confirmar')).toBeInTheDocument()
    expect(puts).toEqual([{ enabled: true }])
    await waitFor(() => expect(status()).toHaveTextContent('para confirmar'))
    expect(await checkbox()).toBeChecked()
  })

  it('turning it on with a confirmed address just activates it', async () => {
    const user = userEvent.setup()
    setup({ initial: settings({ emailConfirmed: true }) })

    await user.click(await checkbox())

    expect(await screen.findByText('Avisos por correo activados')).toBeInTheDocument()
    expect(status()).toHaveTextContent('Activados.')
  })

  it('turning it off is immediate and says so', async () => {
    const user = userEvent.setup()
    const puts = setup({ initial: settings({ budgetEmailAlerts: true, emailConfirmed: true }) })

    await user.click(await checkbox())

    expect(await screen.findByText('Avisos por correo desactivados')).toBeInTheDocument()
    expect(puts).toEqual([{ enabled: false }])
    expect(await checkbox()).not.toBeChecked()
    expect(status()).toHaveTextContent('Apagado.')
  })

  it('stays off and shows the error when the confirmation mail could not be sent (503)', async () => {
    const user = userEvent.setup()
    setup({ putReply: { status: 503, body: { error: 'No se pudo enviar el correo.' } } })

    await user.click(await checkbox())

    expect(await screen.findByText('No se pudo enviar el correo.')).toBeInTheDocument()
    expect(await checkbox()).not.toBeChecked()
    expect(status()).toHaveTextContent('Apagado.')
  })

  it('resends the mail from the waiting state, and shows the wait when it was asked too soon', async () => {
    const user = userEvent.setup()
    const puts = setup({
      initial: settings({ budgetEmailAlerts: true, emailConfirmed: false }),
      putReply: { status: 400, body: { error: 'Esperá un minuto antes de pedir otro correo.' } },
    })

    await user.click(await screen.findByRole('button', { name: 'Reenviar el correo' }))

    expect(await screen.findByText('Esperá un minuto antes de pedir otro correo.')).toBeInTheDocument()
    expect(puts).toEqual([{ enabled: true }])
    // Nothing changed: still waiting for the link
    expect(await checkbox()).toBeChecked()
  })

  it('cannot be pressed twice while it is being saved', async () => {
    const user = userEvent.setup()
    setup()

    const box = await checkbox()
    await user.click(box)

    await waitFor(() => expect(box).toBeEnabled())
  })

  it('shows nothing when the settings could not be loaded', async () => {
    setup({ getReply: { status: 500, body: { error: 'caído' } } })

    await waitFor(() => expect(screen.queryByRole('checkbox')).not.toBeInTheDocument())
    expect(screen.queryByText(/Apagado|Activados|para confirmar/)).not.toBeInTheDocument()
  })
})
