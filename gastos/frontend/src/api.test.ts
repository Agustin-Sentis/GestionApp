import { describe, expect, it, vi } from 'vitest'
import {
  ApiError,
  api,
  loadSession,
  saveSession,
  setUnauthorizedHandler,
} from './api.ts'
import { isBlob } from './test/blob.ts'
import { headersOf, mockFetch, mockFetchNetworkError } from './test/fetchMock.ts'

const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()
const anHourAgo = () => new Date(Date.now() - 60 * 60 * 1000).toISOString()

function signIn() {
  saveSession({ token: 'jwt-token', expiresAtUtc: inOneHour() })
}

describe('requests', () => {
  it('sends credentials as JSON, without an Authorization header when nobody is signed in', async () => {
    const fetchMock = mockFetch(() => ({ body: { token: 't', userId: 'u', email: 'a@x.com', expiresAtUtc: inOneHour() } }))

    await api.signIn('a@x.com', 'Secret1!')

    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/auth\/signin$/)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual({ email: 'a@x.com', password: 'Secret1!' })
    expect(headersOf(init)['content-type']).toBe('application/json')
    expect(headersOf(init)['authorization']).toBeUndefined()
  })

  it('adds the bearer token of the stored session', async () => {
    signIn()
    const fetchMock = mockFetch(() => ({ body: [] }))

    await api.categories.list()

    expect(headersOf(fetchMock.mock.calls[0][1])['authorization']).toBe('Bearer jwt-token')
  })

  it('does not set a content type on requests without a body', async () => {
    const fetchMock = mockFetch(() => ({ body: [] }))

    await api.categories.list()

    expect(headersOf(fetchMock.mock.calls[0][1])['content-type']).toBeUndefined()
  })

  it('builds the query string of the monthly report', async () => {
    const fetchMock = mockFetch(() => ({ body: [] }))

    await api.reports.monthly(2026, 3)

    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/report\/monthly\?year=2026&month=3$/)
  })

  it('asks for a page of transactions with only the filters that are set', async () => {
    const fetchMock = mockFetch(() => ({ body: { items: [], total: 0, page: 2, pageSize: 10, totalPages: 0 } }))

    const result = await api.transactions.listPaged({
      year: 2026,
      month: 11,
      categoryId: 'c1',
      type: 'Expense',
      page: 2,
      pageSize: 10,
    })

    const url = new URL(String(fetchMock.mock.calls[0][0]))
    expect(url.pathname).toBe('/api/transaction/paged')
    expect(Object.fromEntries(url.searchParams)).toEqual({
      year: '2026', month: '11', categoryId: 'c1', type: 'Expense', page: '2', pageSize: '10',
    })
    expect(result.page).toBe(2)
  })

  it('leaves out undefined and empty filters, and the whole query string when there are none', async () => {
    const fetchMock = mockFetch(() => ({ body: { items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 } }))

    await api.transactions.listPaged({ categoryId: '', type: undefined, from: '2026-11-01', to: '2026-11-30' })
    await api.transactions.listPaged()

    const withFilters = new URL(String(fetchMock.mock.calls[0][0]))
    expect(Object.fromEntries(withFilters.searchParams)).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    expect(String(fetchMock.mock.calls[1][0])).toMatch(/\/api\/transaction\/paged$/)
  })

  it('sends the search text of the page request', async () => {
    const fetchMock = mockFetch(() => ({ body: { items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 } }))

    await api.transactions.listPaged({ year: 2026, month: 11, search: '50% off & más' })

    const url = new URL(String(fetchMock.mock.calls[0][0]))
    expect(url.searchParams.get('search')).toBe('50% off & más') // encoded on the way, intact when read back
  })

  it('asks for the CSV export with the filters but without the page, and returns the file', async () => {
    signIn()
    const fetchMock = mockFetch(() => ({ body: 'Fecha;Tipo' }))

    const file = await api.transactions.exportCsv({
      year: 2026, month: 11, categoryId: 'c1', type: 'Expense', search: 'cena', page: 3, pageSize: 20,
    })

    const [rawUrl, init] = fetchMock.mock.calls[0]
    const url = new URL(String(rawUrl))
    expect(url.pathname).toBe('/api/transaction/export')
    expect(Object.fromEntries(url.searchParams)).toEqual({
      year: '2026', month: '11', categoryId: 'c1', type: 'Expense', search: 'cena',
    })
    expect(headersOf(init)['authorization']).toBe('Bearer jwt-token')
    expect(isBlob(file)).toBe(true)
  })

  it('raises the error of the server when the export is refused', async () => {
    mockFetch(() => ({ status: 400, body: { error: 'Hay más de 10000 movimientos para exportar.' } }))

    await expect(api.transactions.exportCsv({ year: 2026, month: 11 })).rejects.toMatchObject({
      status: 400,
      message: 'Hay más de 10000 movimientos para exportar.',
    })
  })

  it('reads the settings of the signed-in user', async () => {
    signIn()
    const fetchMock = mockFetch(() => ({ body: { dollarType: 'Blue' } }))

    const settings = await api.settings.get()

    expect(settings).toEqual({ dollarType: 'Blue' })
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/settings$/)
    expect(init?.method ?? 'GET').toBe('GET')
    expect(headersOf(init)['authorization']).toBe('Bearer jwt-token')
  })

  it.each(['Official', 'Blue', 'Mep'] as const)('saves the %s dollar with PUT, as text', async (dollarType) => {
    signIn()
    const fetchMock = mockFetch(() => ({ body: { dollarType } }))

    const saved = await api.settings.update(dollarType)

    expect(saved).toEqual({ dollarType })
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/settings$/)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual({ dollarType }) // the enum travels as its name, not as a number
  })

  it('raises the error of the server when the dollar is refused', async () => {
    signIn()
    mockFetch(() => ({ status: 400, body: { error: 'El tipo de dólar no es válido.' } }))

    await expect(api.settings.update('Blue')).rejects.toMatchObject({ status: 400, message: 'El tipo de dólar no es válido.' })
  })

  it('asks for the recovery mail with POST and the email, with no session needed', async () => {
    const fetchMock = mockFetch(() => ({ status: 204 }))

    await api.forgotPassword('ana@example.com')

    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/auth\/forgot-password$/)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual({ email: 'ana@example.com' })
    expect(headersOf(init)['authorization']).toBeUndefined()
  })

  it('raises the 429 of the server when the recovery is asked for too many times', async () => {
    mockFetch(() => ({ status: 429, body: { error: 'Demasiados intentos. Probá de nuevo en unos minutos.' } }))

    await expect(api.forgotPassword('a@x.com')).rejects.toMatchObject({ status: 429 })
  })

  it('sets the new password with POST, the token and the password, with no session needed', async () => {
    const fetchMock = mockFetch(() => ({ status: 204 }))

    await api.resetPassword('abc_-123', 'Nueva1234!')

    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/auth\/reset-password$/)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual({ token: 'abc_-123', newPassword: 'Nueva1234!' })
    expect(headersOf(init)['authorization']).toBeUndefined()
  })

  it('raises the 400 with the text of the server when the link expired or the password breaks the rules', async () => {
    mockFetch(() => ({ status: 400, body: { error: 'El enlace venció o ya se usó.' } }))

    await expect(api.resetPassword('x', 'Nueva1234!')).rejects.toMatchObject({ status: 400, message: 'El enlace venció o ya se usó.' })
  })

  it.each([true, false])('turns the budget alert mails %s with PUT, with the session', async (enabled) => {
    signIn()
    const fetchMock = mockFetch(() => ({ body: { budgetEmailAlerts: enabled } }))

    const saved = await api.settings.setBudgetAlerts(enabled)

    expect(saved).toEqual({ budgetEmailAlerts: enabled })
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/settings\/budget-alerts$/)
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(String(init?.body))).toEqual({ enabled })
    expect(headersOf(init)['authorization']).toBe('Bearer jwt-token')
  })

  it('raises the 503 of the server when the confirmation mail could not be sent', async () => {
    signIn()
    mockFetch(() => ({ status: 503, body: { error: 'No se pudo enviar el correo.' } }))

    await expect(api.settings.setBudgetAlerts(true)).rejects.toMatchObject({ status: 503, message: 'No se pudo enviar el correo.' })
  })

  it('confirms the email with POST and the token, with no session needed', async () => {
    // Nobody signed in: the link of the mail may be opened on another device
    const fetchMock = mockFetch(() => ({ status: 204 }))

    await api.settings.confirmEmail('abc_-123')

    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toMatch(/\/api\/settings\/confirm-email$/)
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual({ token: 'abc_-123' })
    expect(headersOf(init)['authorization']).toBeUndefined()
  })

  it('raises the 400 when the confirmation link expired or was used', async () => {
    mockFetch(() => ({ status: 400, body: { error: 'El enlace venció o ya se usó.' } }))

    await expect(api.settings.confirmEmail('x')).rejects.toMatchObject({ status: 400, message: 'El enlace venció o ya se usó.' })
  })

  it('fetches one transaction by id', async () => {
    const fetchMock = mockFetch(() => ({ body: { id: 't1', exchangeRate: 1570, arsAmount: 15700 } }))

    const transaction = await api.transactions.get('t1')

    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/transaction\/t1$/)
    expect(transaction.arsAmount).toBe(15700)
  })

  it('uses POST to create, PUT to update and DELETE to remove a transaction', async () => {
    const fetchMock = mockFetch(() => ({ status: 204 }))
    const input = { amount: 10, currency: 'ARS' as const, date: '2026-11-05', note: null, categoryId: 'c1' }

    await api.transactions.create(input)
    await api.transactions.update('t1', input)
    await api.transactions.remove('t1')

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method} ${new URL(String(url)).pathname}`)
    expect(calls).toEqual(['POST /api/transaction', 'PUT /api/transaction/t1', 'DELETE /api/transaction/t1'])
  })

  it('returns undefined for a 204 response', async () => {
    mockFetch(() => ({ status: 204 }))

    await expect(api.transactions.remove('t1')).resolves.toBeUndefined()
  })
})

describe('errors', () => {
  it('uses the "error" message of the API and turns the validation separator into spaces', async () => {
    mockFetch(() => ({ status: 400, body: { error: 'El email es obligatorio.~La contraseña es obligatoria.' } }))

    const error = await api.signIn('', '').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).message).toBe('El email es obligatorio. La contraseña es obligatoria.')
  })

  it('joins the messages of an ASP.NET validation problem', async () => {
    mockFetch(() => ({ status: 400, body: { title: 'One or more validation errors occurred.', errors: { Page: ['bad page'], Type: ['bad type'] } } }))

    await expect(api.categories.list()).rejects.toThrow('bad page bad type')
  })

  it('falls back to the title of a problem without field errors', async () => {
    mockFetch(() => ({ status: 400, body: { title: 'Bad Request' } }))

    await expect(api.categories.list()).rejects.toThrow('Bad Request')
  })

  it('falls back to a generic message when the body is not JSON', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('<html>oops</html>', { status: 502 })))

    await expect(api.categories.list()).rejects.toThrow('Error 502')
  })

  it('reports an unreachable server with status 0', async () => {
    mockFetchNetworkError()

    const error = await api.categories.list().catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(0)
    expect((error as ApiError).message).toBe('No se pudo conectar con el servidor')
  })
})

describe('expired session handling', () => {
  it('calls the unauthorized handler on a 401 when there was a session', async () => {
    signIn()
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    mockFetch(() => ({ status: 401, body: { error: 'La sesión no es válida.' } }))

    await expect(api.categories.list()).rejects.toThrow('La sesión no es válida.')

    expect(handler).toHaveBeenCalledTimes(1)
    setUnauthorizedHandler(null)
  })

  it('does not call it on a 401 without a session (a wrong password is not an expired login)', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    mockFetch(() => ({ status: 401, body: { error: 'Credenciales inválidas.' } }))

    await expect(api.signIn('a@x.com', 'wrong')).rejects.toThrow('Credenciales inválidas.')

    expect(handler).not.toHaveBeenCalled()
    setUnauthorizedHandler(null)
  })
})

describe('stored session', () => {
  it('round-trips a valid session', () => {
    const session = { token: 'abc', expiresAtUtc: inOneHour() }

    saveSession(session)

    expect(loadSession()).toEqual(session)
  })

  it('discards an expired session and removes it from storage', () => {
    saveSession({ token: 'abc', expiresAtUtc: anHourAgo() })

    expect(loadSession()).toBeNull()
    expect(localStorage.getItem('gastos.session')).toBeNull()
  })

  it('ignores corrupted storage instead of crashing', () => {
    localStorage.setItem('gastos.session', '{not json')

    expect(loadSession()).toBeNull()
  })

  it('ignores a session without token', () => {
    localStorage.setItem('gastos.session', JSON.stringify({ expiresAtUtc: inOneHour() }))

    expect(loadSession()).toBeNull()
  })

  it('removes the session when asked to save null', () => {
    signIn()

    saveSession(null)

    expect(loadSession()).toBeNull()
  })
})
