import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import type { DollarType } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import { renderWithProviders } from '../test/render.tsx'
import DollarTypeToggle from './DollarTypeToggle.tsx'

function setup(initial: DollarType = 'Official', routes: Parameters<typeof mockApi>[0] = {}) {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  let saved = initial
  const fetchMock = mockApi({
    'GET /api/settings': () => ({ body: { dollarType: saved } }),
    'PUT /api/settings': (init) => {
      saved = JSON.parse(String(init.body)).dollarType
      return { body: { dollarType: saved } }
    },
    'GET /api/report/monthly': () => ({ body: {} }),
    ...routes,
  })
  const view = renderWithProviders(<DollarTypeToggle />)
  return { fetchMock, ...view }
}

const choice = (name: string) => screen.getByRole('radio', { name })

/** The bodies of the requests made with this method. */
const bodiesOf = (fetchMock: ReturnType<typeof mockApi>, method: string) =>
  fetchMock.mock.calls.filter(([, init]) => init?.method === method).map(([, init]) => JSON.parse(String(init?.body)))

describe('DollarTypeToggle', () => {
  it('shows the three dollars at once, as a group called "Cotización del dólar"', async () => {
    setup()

    expect(await screen.findByRole('group', { name: 'Dólar con el que se guardan los movimientos nuevos' })).toBeInTheDocument()
    expect(screen.getAllByRole('radio').map((radio) => radio.getAttribute('value'))).toEqual(['Official', 'Blue', 'Mep'])
    expect(choice('Oficial')).toBeInTheDocument()
    expect(choice('Blue')).toBeInTheDocument()
    expect(choice('MEP')).toBeInTheDocument()
  })

  it.each([
    ['Official', 'Oficial'],
    ['Blue', 'Blue'],
    ['Mep', 'MEP'],
  ] as const)('starts on the dollar the user chose before: %s', async (saved, label) => {
    setup(saved)

    await waitFor(() => expect(choice(label)).toBeChecked())
  })

  it('cannot be changed until the saved choice has arrived', async () => {
    setup()

    expect(choice('Blue')).toBeDisabled()
    await waitFor(() => expect(choice('Blue')).toBeEnabled())
  })

  it('saves the new dollar, shows it, and says so', async () => {
    const { fetchMock } = setup('Official')
    await waitFor(() => expect(choice('Blue')).toBeEnabled())

    await userEvent.click(choice('Blue'))

    await waitFor(() => expect(choice('Blue')).toBeChecked())
    expect(bodiesOf(fetchMock, 'PUT')).toEqual([{ dollarType: 'Blue' }])
    expect(await screen.findByText('Los movimientos nuevos se guardan con el dólar Blue')).toBeInTheDocument()
  })

  it('does not ask again for the report or the list: nothing already saved changes with the choice', async () => {
    const { queryClient, fetchMock } = setup('Official')
    await waitFor(() => expect(choice('MEP')).toBeEnabled())
    queryClient.setQueryData(['report', 2026, 11], { fresh: true })
    queryClient.setQueryData(['transactions', { page: 1 }], { fresh: true })

    await userEvent.click(choice('MEP'))
    await waitFor(() => expect(choice('MEP')).toBeChecked())

    // The figures and the movements were saved with their own dollar: they stay exactly as they are
    expect(queryClient.getQueryState(['report', 2026, 11])?.isInvalidated).toBe(false)
    expect(queryClient.getQueryState(['transactions', { page: 1 }])?.isInvalidated).toBe(false)
    const asked = fetchMock.mock.calls.map(([url]) => new URL(String(url)).pathname)
    expect(asked.filter((path) => path.includes('report') || path.includes('transaction'))).toEqual([])
  })

  it('does not change the saved choice when the server refuses, and shows why', async () => {
    const { fetchMock } = setup('Official', {
      'PUT /api/settings': () => ({ status: 400, body: { error: 'El tipo de dólar no es válido.' } }),
    })
    await waitFor(() => expect(choice('Blue')).toBeEnabled())

    await userEvent.click(choice('Blue'))

    expect(await screen.findByText('El tipo de dólar no es válido.')).toBeInTheDocument()
    await waitFor(() => expect(choice('Oficial')).toBeChecked()) // back to what is really saved
    expect(bodiesOf(fetchMock, 'PUT')).toHaveLength(1)
  })

  it('stays disabled, without breaking, when the saved choice cannot be loaded', async () => {
    setup('Official', { 'GET /api/settings': () => ({ status: 500, body: { error: 'caído' } }) })

    await waitFor(() => expect(choice('Blue')).toBeDisabled())
    expect(choice('Oficial')).not.toBeChecked() // nothing is claimed that is not known
  })

  it('moves between the dollars with the arrow keys', async () => {
    setup('Official')
    await waitFor(() => expect(choice('Oficial')).toBeChecked())
    await waitFor(() => expect(choice('Oficial')).toBeEnabled())

    choice('Oficial').focus()
    await userEvent.keyboard('{ArrowRight}')

    await waitFor(() => expect(choice('Blue')).toBeChecked())
  })

  it('explains each dollar in a note that is also its description for screen readers', async () => {
    setup()
    await screen.findByRole('group', { name: 'Dólar con el que se guardan los movimientos nuevos' })

    expect(screen.getAllByRole('tooltip')).toHaveLength(3)
    expect(choice('Oficial')).toHaveAccessibleDescription(/El dólar oficial/)
    expect(choice('Blue')).toHaveAccessibleDescription(/mercado informal/)
    expect(choice('MEP')).toHaveAccessibleDescription(/también llamado bolsa/)
  })

  it('says in every note that the choice is for the movements loaded from now on, and that the saved ones keep theirs', async () => {
    setup()
    await screen.findByRole('group', { name: 'Dólar con el que se guardan los movimientos nuevos' })

    for (const note of screen.getAllByRole('tooltip')) {
      expect(note).toHaveTextContent('Se usa para los movimientos que cargues de ahora en adelante')
      expect(note).toHaveTextContent('los que ya cargaste conservan el suyo')
    }
  })

  it('says it too in the description that screen readers get', async () => {
    setup()
    await screen.findByRole('group', { name: 'Dólar con el que se guardan los movimientos nuevos' })

    expect(choice('Blue')).toHaveAccessibleDescription(/los que ya cargaste conservan el suyo/)
  })

  it('keeps the note out of the name of the choice, and out of the way of the frame', async () => {
    setup()
    const group = await screen.findByRole('group', { name: 'Dólar con el que se guardan los movimientos nuevos' })

    expect(choice('Oficial')).toBeInTheDocument() // "Oficial", not "Oficial El dólar oficial…"
    const note = screen.getAllByRole('tooltip')[0]
    expect(note.className).toContain('invisible')
    expect(note.className).toContain('group-hover:visible')
    expect(note.className).toContain('group-has-focus-visible:visible')
    expect(group.className).not.toContain('overflow')
  })
})
