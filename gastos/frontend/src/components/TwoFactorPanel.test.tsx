import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { saveSession } from '../api.ts'
import { downloadBlob } from '../download.ts'
import { anyBlob } from '../test/blob.ts'
import { mockApi } from '../test/fetchMock.ts'
import type { MockReply } from '../test/fetchMock.ts'
import { settings } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import TwoFactorPanel from './TwoFactorPanel.tsx'

vi.mock('../download.ts', () => ({ downloadBlob: vi.fn() }))

const CODES = ['AAAAA-BBBBB', 'CCCCC-DDDDD', 'EEEEE-FFFFF', 'GGGGG-HHHHH', 'JJJJJ-KKKKK', 'LLLLL-MMMMM', 'NNNNN-PPPPP', 'QQQQQ-RRRRR', 'SSSSS-TTTTT', 'UUUUU-VVVVV']
const SETUP = { uri: 'otpauth://totp/DualBalance:ana%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=DualBalance', secret: 'JBSWY3DPEHPK3PXP' }

interface Options {
  enabled?: boolean
  setup?: MockReply
  enable?: MockReply
  disable?: MockReply
  regenerate?: MockReply
}

/** A fake server that remembers whether the 2FA is on, and returns the bodies it received, by endpoint. */
function setup({ enabled = false, ...replies }: Options = {}) {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  let on = enabled
  const sent: Record<string, unknown[]> = { setup: [], enable: [], disable: [], recovery: [] }
  mockApi({
    'GET /api/settings': () => ({ body: settings({ twoFactorEnabled: on }) }),
    'POST /api/settings/2fa/setup': (init) => {
      sent.setup.push(JSON.parse(String(init.body)))
      return replies.setup ?? { body: SETUP }
    },
    'POST /api/settings/2fa/enable': (init) => {
      sent.enable.push(JSON.parse(String(init.body)))
      if (replies.enable) return replies.enable
      on = true
      return { body: { codes: CODES } }
    },
    'POST /api/settings/2fa/disable': (init) => {
      sent.disable.push(JSON.parse(String(init.body)))
      if (replies.disable) return replies.disable
      on = false
      return { status: 204 }
    },
    'POST /api/settings/2fa/recovery-codes': (init) => {
      sent.recovery.push(JSON.parse(String(init.body)))
      return replies.regenerate ?? { body: { codes: CODES } }
    },
  })
  const onBlockingChange = vi.fn()
  const user = userEvent.setup()
  renderWithProviders(<TwoFactorPanel onBlockingChange={onBlockingChange} />)
  return { sent, onBlockingChange, user }
}

type User = ReturnType<typeof userEvent.setup>

async function typeInto(user: User, label: string | RegExp, text: string) {
  await user.type(screen.getByLabelText(label), text)
}

/** Off -> password -> scan, as far as the QR. */
async function goToScan(user: User) {
  await user.click(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' }))
  await typeInto(user, 'Tu contraseña', 'Demo1234!')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  await screen.findByRole('img', { name: /código QR/i })
}

/** All the way to the recovery codes. */
async function goToCodes(user: User) {
  await goToScan(user)
  await user.click(screen.getByRole('button', { name: 'Siguiente' }))
  await typeInto(user, 'Código de 6 dígitos', '123456')
  await user.click(screen.getByRole('button', { name: 'Activar' }))
  await screen.findByText(CODES[0])
}

beforeEach(() => {
  vi.mocked(downloadBlob).mockClear()
})

describe('TwoFactorPanel, turned off', () => {
  it('explains it and offers to turn it on, without showing any secret', async () => {
    setup()

    expect(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' })).toBeInTheDocument()
    expect(screen.getByText(/app autenticadora/i)).toBeInTheDocument()
    expect(screen.queryByRole('img', { name: /código QR/i })).not.toBeInTheDocument()
    expect(screen.queryByText('Activada')).not.toBeInTheDocument()
  })

  it('asks for the password first, and sends it', async () => {
    const { sent, user } = setup()
    await user.click(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' }))

    await typeInto(user, 'Tu contraseña', 'Demo1234!')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    await screen.findByRole('img', { name: /código QR/i })
    expect(sent.setup).toEqual([{ password: 'Demo1234!' }])
  })

  it('says a wrong password inside the form, and stays there', async () => {
    const { user } = setup({ setup: { status: 400, body: { error: 'Contraseña incorrecta.' } } })
    await user.click(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' }))
    await typeInto(user, 'Tu contraseña', 'mala')

    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Contraseña incorrecta.')
    expect(screen.getByLabelText('Tu contraseña')).toBeInTheDocument()
  })

  it('shows the lock when there were too many tries', async () => {
    const { user } = setup({ setup: { status: 429, body: { error: 'Demasiados intentos. Probá de nuevo en 12 minutos.' } } })
    await user.click(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' }))
    await typeInto(user, 'Tu contraseña', 'Demo1234!')

    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiados intentos. Probá de nuevo en 12 minutos.')
  })

  it('cancels from the password form', async () => {
    const { user } = setup()
    await user.click(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' }))

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(screen.getByRole('button', { name: 'Activar verificación en dos pasos' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Tu contraseña')).not.toBeInTheDocument()
  })

  it('does not let a blank password be sent', async () => {
    const { sent, user } = setup()
    await user.click(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' }))

    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(sent.setup).toEqual([])
    expect(screen.getByLabelText('Tu contraseña')).toBeInTheDocument()
  })
})

describe('TwoFactorPanel, the three steps', () => {
  it('step 1 shows the QR and the key as text', async () => {
    const { user } = setup()

    await goToScan(user)

    expect(screen.getByText(/Paso 1 de 3/)).toBeInTheDocument()
    expect(screen.getByText(SETUP.secret)).toBeInTheDocument()
    expect(screen.getByText(/Google Authenticator/)).toBeInTheDocument()
  })

  it('copies the key', async () => {
    const { user } = setup()
    await goToScan(user)

    await user.click(screen.getByRole('button', { name: 'Copiar clave' }))

    expect(await navigator.clipboard.readText()).toBe(SETUP.secret)
  })

  it('step 2 asks for the first code, and sends it', async () => {
    const { sent, user } = setup()
    await goToScan(user)

    await user.click(screen.getByRole('button', { name: 'Siguiente' }))

    expect(screen.getByText(/Paso 2 de 3/)).toBeInTheDocument()
    const input = screen.getByLabelText('Código de 6 dígitos')
    expect(input).toHaveAttribute('inputmode', 'numeric')
    expect(input).toHaveAttribute('autocomplete', 'one-time-code')
    await user.type(input, '123456')
    await user.click(screen.getByRole('button', { name: 'Activar' }))
    await screen.findByText(CODES[0])
    expect(sent.enable).toEqual([{ code: '123456' }])
  })

  it('can go back from the code to the QR', async () => {
    const { user } = setup()
    await goToScan(user)
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))

    await user.click(screen.getByRole('button', { name: 'Atrás' }))

    expect(screen.getByRole('img', { name: /código QR/i })).toBeInTheDocument()
  })

  it('says a wrong code and lets the user type it again', async () => {
    const { user } = setup({ enable: { status: 400, body: { error: 'Código incorrecto o vencido.' } } })
    await goToScan(user)
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    await typeInto(user, 'Código de 6 dígitos', '000000')

    await user.click(screen.getByRole('button', { name: 'Activar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Código incorrecto o vencido.')
    expect(screen.getByText(/Paso 2 de 3/)).toBeInTheDocument()
    expect(screen.queryByText(CODES[0])).not.toBeInTheDocument()
  })

  it('goes back to the start when the activation was not started (409)', async () => {
    const { user } = setup({ enable: { status: 409, body: { error: 'Primero iniciá la activación.' } } })
    await goToScan(user)
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    await typeInto(user, 'Código de 6 dígitos', '123456')

    await user.click(screen.getByRole('button', { name: 'Activar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Primero iniciá la activación.')
    expect(screen.getByRole('button', { name: 'Activar verificación en dos pasos' })).toBeInTheDocument()
  })

  it('step 3 shows the ten codes, blocks closing, and only lets go after the user says they saved them', async () => {
    const { onBlockingChange, user } = setup()

    await goToCodes(user)

    expect(screen.getByText(/Paso 3 de 3/)).toBeInTheDocument()
    expect(screen.getAllByRole('listitem')).toHaveLength(10)
    expect(onBlockingChange).toHaveBeenLastCalledWith(true)
    const done = screen.getByRole('button', { name: 'Listo' })
    expect(done).toBeDisabled()

    await user.click(screen.getByRole('checkbox', { name: /Ya guardé los códigos/ }))
    expect(done).toBeEnabled()
    await user.click(done)

    expect(onBlockingChange).toHaveBeenLastCalledWith(false)
    // They are not shown again, and the panel now says it is on
    expect(screen.queryByText(CODES[0])).not.toBeInTheDocument()
    expect(await screen.findByText('Activada')).toBeInTheDocument()
  })

  it('copies the codes, one per line', async () => {
    const { user } = setup()
    await goToCodes(user)

    await user.click(screen.getByRole('button', { name: 'Copiar códigos' }))

    expect(await navigator.clipboard.readText()).toBe(CODES.join('\n'))
  })

  it('downloads the codes as a text file', async () => {
    const { user } = setup()
    await goToCodes(user)

    await user.click(screen.getByRole('button', { name: 'Descargar .txt' }))

    expect(downloadBlob).toHaveBeenCalledWith(anyBlob(), 'codigos-de-recuperacion-dualbalance.txt')
    const [blob] = vi.mocked(downloadBlob).mock.calls[0]
    expect(blob.size).toBeGreaterThan(CODES.join('\n').length)
  })
})

describe('TwoFactorPanel, turned on', () => {
  it('says it is on and offers the two actions, nothing else', async () => {
    setup({ enabled: true })

    expect(await screen.findByText('Activada')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Generar códigos de recuperación nuevos' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Desactivar verificación en dos pasos' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Activar verificación en dos pasos' })).not.toBeInTheDocument()
  })

  it('replaces the recovery codes with the password and a code, and shows the new ones', async () => {
    const { sent, onBlockingChange, user } = setup({ enabled: true })
    await user.click(await screen.findByRole('button', { name: 'Generar códigos de recuperación nuevos' }))

    await typeInto(user, 'Tu contraseña', 'Demo1234!')
    await typeInto(user, /Código de la app o de recuperación/, '123456')
    await user.click(screen.getByRole('button', { name: 'Generar códigos nuevos' }))

    await screen.findByText(CODES[0])
    expect(sent.recovery).toEqual([{ password: 'Demo1234!', code: '123456' }])
    expect(onBlockingChange).toHaveBeenLastCalledWith(true)
    expect(screen.getAllByRole('listitem')).toHaveLength(10)
  })

  it('turns it off with the password and a code', async () => {
    const { sent, user } = setup({ enabled: true })
    await user.click(await screen.findByRole('button', { name: 'Desactivar verificación en dos pasos' }))

    await typeInto(user, 'Tu contraseña', 'Demo1234!')
    await typeInto(user, /Código de la app o de recuperación/, 'AAAAA-BBBBB')
    await user.click(screen.getByRole('button', { name: 'Desactivar' }))

    expect(await screen.findByRole('button', { name: 'Activar verificación en dos pasos' })).toBeInTheDocument()
    expect(sent.disable).toEqual([{ password: 'Demo1234!', code: 'AAAAA-BBBBB' }])
    expect(screen.queryByText('Activada')).not.toBeInTheDocument()
  })

  it('keeps it on, and says why, when the code is wrong', async () => {
    const { user } = setup({ enabled: true, disable: { status: 400, body: { error: 'Código incorrecto o vencido.' } } })
    await user.click(await screen.findByRole('button', { name: 'Desactivar verificación en dos pasos' }))
    await typeInto(user, 'Tu contraseña', 'Demo1234!')
    await typeInto(user, /Código de la app o de recuperación/, '000000')

    await user.click(screen.getByRole('button', { name: 'Desactivar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Código incorrecto o vencido.')
    expect(screen.getByText('Activada')).toBeInTheDocument()
  })

  it('shows the lock when there were too many wrong codes', async () => {
    const { user } = setup({ enabled: true, regenerate: { status: 429, body: { error: 'Demasiados intentos. Probá de nuevo en 15 minutos.' } } })
    await user.click(await screen.findByRole('button', { name: 'Generar códigos de recuperación nuevos' }))
    await typeInto(user, 'Tu contraseña', 'Demo1234!')
    await typeInto(user, /Código de la app o de recuperación/, '123456')

    await user.click(screen.getByRole('button', { name: 'Generar códigos nuevos' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Demasiados intentos. Probá de nuevo en 15 minutos.')
  })

  it('cancels an action, and clears what was typed', async () => {
    const { user } = setup({ enabled: true })
    await user.click(await screen.findByRole('button', { name: 'Desactivar verificación en dos pasos' }))
    await typeInto(user, 'Tu contraseña', 'Demo1234!')

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    await user.click(screen.getByRole('button', { name: 'Desactivar verificación en dos pasos' }))

    expect(screen.getByLabelText('Tu contraseña')).toHaveValue('')
  })

  it('asks for both fields before sending', async () => {
    const { sent, user } = setup({ enabled: true })
    await user.click(await screen.findByRole('button', { name: 'Desactivar verificación en dos pasos' }))
    await typeInto(user, 'Tu contraseña', 'Demo1234!')

    await user.click(screen.getByRole('button', { name: 'Desactivar' }))

    expect(sent.disable).toEqual([])
    await waitFor(() => expect(within(screen.getByRole('alert')).getByText(/contraseña y un código/i)).toBeInTheDocument())
  })
})
