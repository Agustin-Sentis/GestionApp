import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { saveSession } from '../api.ts'
import type { Settings } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import { settings } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import SettingsModal from './SettingsModal.tsx'
import type { SettingsSection } from './SettingsModal.tsx'

vi.mock('../download.ts', () => ({ downloadBlob: vi.fn() }))

const CODES = ['AAAAA-BBBBB', 'CCCCC-DDDDD', 'EEEEE-FFFFF', 'GGGGG-HHHHH', 'JJJJJ-KKKKK', 'LLLLL-MMMMM', 'NNNNN-PPPPP', 'QQQQQ-RRRRR', 'SSSSS-TTTTT', 'UUUUU-VVVVV']

/** What the dashboard does: holds whether it is open and which section, and opens it from a button. */
function Harness({ initial = 'security', startOpen = false }: { initial?: SettingsSection; startOpen?: boolean }) {
  const [open, setOpen] = useState(startOpen)
  const [section, setSection] = useState<SettingsSection>(initial)
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>Abrir ajustes</button>
      <SettingsModal open={open} section={section} onSectionChange={setSection} onClose={() => setOpen(false)} />
    </>
  )
}

function setup(saved: Settings = settings(), props: { initial?: SettingsSection; startOpen?: boolean } = {}) {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  let current = saved
  mockApi({
    'GET /api/settings': () => ({ body: current }),
    'POST /api/settings/2fa/setup': () => ({ body: { uri: 'otpauth://totp/x?secret=ABC', secret: 'ABC' } }),
    'POST /api/settings/2fa/enable': () => {
      current = { ...current, twoFactorEnabled: true }
      return { body: { codes: CODES } }
    },
  })
  const user = userEvent.setup()
  renderWithProviders(<Harness {...props} />)
  return { user }
}

const open = (user: ReturnType<typeof userEvent.setup>) => user.click(screen.getByRole('button', { name: 'Abrir ajustes' }))

describe('SettingsModal', () => {
  it('shows nothing while it is closed', () => {
    setup()

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('opens as a modal dialog called Ajustes, with its two sections', async () => {
    const { user } = setup()

    await open(user)

    const dialog = screen.getByRole('dialog', { name: 'Ajustes' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(screen.getByRole('tab', { name: 'Seguridad' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tab', { name: 'Notificaciones' })).toHaveAttribute('aria-selected', 'false')
    expect(await screen.findByRole('heading', { name: 'Verificación en dos pasos' })).toBeInTheDocument()
  })

  it('opens on the section it was asked for', async () => {
    const { user } = setup(settings(), { initial: 'notifications' })

    await open(user)

    expect(screen.getByRole('tab', { name: 'Notificaciones' })).toHaveAttribute('aria-selected', 'true')
    expect(await screen.findByRole('checkbox', { name: /Avisarme por correo/ })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Verificación en dos pasos' })).not.toBeInTheDocument()
  })

  it('changes section', async () => {
    const { user } = setup()
    await open(user)

    await user.click(screen.getByRole('tab', { name: 'Notificaciones' }))

    expect(await screen.findByRole('checkbox', { name: /Avisarme por correo/ })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Notificaciones' })).toHaveAttribute('aria-selected', 'true')
  })

  it('in notifications, says the address is confirmed when it is', async () => {
    const { user } = setup(settings({ emailConfirmed: true }), { initial: 'notifications' })
    await open(user)

    expect(await screen.findByText('Confirmado')).toBeInTheDocument()
    expect(screen.getByText('ana@example.com')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reenviar el correo' })).not.toBeInTheDocument()
  })

  it('in notifications, offers to resend or correct an unconfirmed address', async () => {
    const { user } = setup(settings({ emailConfirmed: false }), { initial: 'notifications' })
    await open(user)

    expect(await screen.findByText('Sin confirmar')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reenviar el correo' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '¿Te equivocaste de correo?' })).toBeInTheDocument()
  })
})

describe('SettingsModal, closing', () => {
  it('closes with Escape, and gives the focus back to what opened it', async () => {
    const { user } = setup()
    await open(user)

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Abrir ajustes' })).toHaveFocus()
  })

  it('closes with its close button', async () => {
    const { user } = setup()
    await open(user)

    await user.click(screen.getByRole('button', { name: 'Cerrar ajustes' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('closes with a click outside the window, but not with a click inside it', async () => {
    const { user } = setup()
    await open(user)

    await user.click(screen.getByRole('dialog', { name: 'Ajustes' }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    await user.click(screen.getByTestId('settings-backdrop'))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('moves the focus into the window when it opens', async () => {
    const { user } = setup()

    await open(user)

    expect(screen.getByRole('dialog', { name: 'Ajustes' }).contains(document.activeElement)).toBe(true)
  })

  it('keeps the Tab key inside the window', async () => {
    const { user } = setup(settings(), { startOpen: true })
    const dialog = await screen.findByRole('dialog', { name: 'Ajustes' })
    await screen.findByRole('heading', { name: 'Verificación en dos pasos' })

    // Tab as many times as there are things to focus, and a few more: the focus never leaves the window
    for (let i = 0; i < 12; i++) {
      await user.tab()
      expect(dialog.contains(document.activeElement)).toBe(true)
    }
    for (let i = 0; i < 12; i++) {
      await user.tab({ shift: true })
      expect(dialog.contains(document.activeElement)).toBe(true)
    }
  })
})

describe('SettingsModal, while the recovery codes are on screen', () => {
  async function toTheCodes(user: ReturnType<typeof userEvent.setup>) {
    await open(user)
    await user.click(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' }))
    await user.type(screen.getByLabelText('Tu contraseña'), 'Demo1234!')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    await screen.findByRole('img', { name: /código QR/i })
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    await user.type(screen.getByLabelText('Código de 6 dígitos'), '123456')
    await user.click(screen.getByRole('button', { name: 'Activar' }))
    await screen.findByText(CODES[0])
  }

  it('cannot be closed by accident: not with Escape, not outside, not with the button', async () => {
    const { user } = setup()
    await toTheCodes(user)

    await user.keyboard('{Escape}')
    await user.click(screen.getByTestId('settings-backdrop'))

    expect(screen.getByRole('dialog', { name: 'Ajustes' })).toBeInTheDocument()
    expect(screen.getByText(CODES[0])).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cerrar ajustes' })).toBeDisabled()
  })

  it('cannot leave the section either', async () => {
    const { user } = setup()
    await toTheCodes(user)

    expect(screen.getByRole('tab', { name: 'Notificaciones' })).toBeDisabled()
  })

  it('can be closed once the user says they saved the codes', async () => {
    const { user } = setup()
    await toTheCodes(user)

    await user.click(screen.getByRole('checkbox', { name: /Ya guardé los códigos/ }))
    await user.click(screen.getByRole('button', { name: 'Listo' }))

    expect(screen.getByRole('button', { name: 'Cerrar ajustes' })).toBeEnabled()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
