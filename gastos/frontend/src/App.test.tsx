import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { saveSession } from './api.ts'
import App from './App.tsx'
import { mockApi } from './test/fetchMock.ts'
import { food, pageOf } from './test/fixtures.ts'
import { renderWithProviders } from './test/render.tsx'

// The chart library draws nothing useful in jsdom (it has no size)
vi.mock('./components/ReportChart.tsx', () => ({ default: () => <div data-testid="chart" /> }))

function signedIn() {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
}

function mockDashboardApi() {
  mockApi({
    'GET /api/settings': () => ({ body: { dollarType: 'Official' } }),
    'GET /api/category': () => ({ body: [food] }),
    'GET /api/report/monthly': () => ({
      body: {
        year: 2026, month: 11, currency: 'ARS', income: 0, expense: 0, balance: 0, byCategory: [], budgets: [],
        pendingConversions: [], hasUnconvertedUsd: false, previous: null,
      },
    }),
    'GET /api/transaction/paged': () => ({ body: pageOf([]) }),
  })
}

describe('App routes', () => {
  it('shows the landing page at the root to someone who has not signed in', () => {
    renderWithProviders(<App />, { route: '/' })

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Anotá lo que gastás.')
    expect(screen.queryByRole('button', { name: 'Salir' })).not.toBeInTheDocument()
  })

  it('shows the dashboard at the same address once signed in', async () => {
    signedIn()
    mockDashboardApi()

    renderWithProviders(<App />, { route: '/?mes=2026-11' })

    expect(await screen.findByRole('button', { name: 'Salir' })).toBeInTheDocument()
    expect(screen.getByLabelText('Mes a visualizar')).toHaveValue('2026-11') // an old link with ?mes= still works
    expect(screen.queryByText('Anotá lo que gastás.')).not.toBeInTheDocument()
  })

  it('shows the dashboard at /panel to someone who is signed in', async () => {
    signedIn()
    mockDashboardApi()

    renderWithProviders(<App />, { route: '/panel?mes=2026-11' })

    expect(await screen.findByRole('button', { name: 'Salir' })).toBeInTheDocument()
    expect(screen.getByLabelText('Mes a visualizar')).toHaveValue('2026-11')
  })

  it('sends someone who has not signed in from /panel to the sign-in form', () => {
    renderWithProviders(<App />, { route: '/panel?mes=2026-09' })

    expect(screen.getByText('Ingresá a tu cuenta')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Salir' })).not.toBeInTheDocument()
  })

  it('takes them back to the same page, with the same month, once they sign in', async () => {
    mockApi({
      'POST /api/auth/signin': () => ({
        body: {
          requiresTwoFactor: false, token: 'jwt', userId: 'u1', email: 'ana@example.com',
          expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
        },
      }),
      'GET /api/settings': () => ({ body: { dollarType: 'Official' } }),
      'GET /api/category': () => ({ body: [food] }),
      'GET /api/report/monthly': () => ({
        body: {
          year: 2026, month: 9, currency: 'ARS', income: 0, expense: 0, balance: 0, byCategory: [], budgets: [],
          pendingConversions: [], hasUnconvertedUsd: false, previous: null,
        },
      }),
      'GET /api/transaction/paged': () => ({ body: pageOf([]) }),
    })
    renderWithProviders(<App />, { route: '/panel?mes=2026-09' })

    await userEvent.type(screen.getByLabelText('Email'), 'ana@example.com')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Demo1234!')
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByRole('button', { name: 'Salir' })).toBeInTheDocument()
    expect(screen.getByLabelText('Mes a visualizar')).toHaveValue('2026-09')
  })

  it('leaves the dashboard for the landing page, not for the sign-in form, when someone chooses to sign out', async () => {
    signedIn()
    mockDashboardApi()
    renderWithProviders(<App />, { route: '/panel' })

    await userEvent.click(await screen.findByRole('button', { name: 'Salir' }))

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Anotá lo que gastás.')
  })

  it('sends someone whose session the server closed to the sign-in form, not to the landing page', async () => {
    signedIn()
    mockApi({
      'GET /api/settings': () => ({ status: 401, body: { error: 'La sesión ya no es válida.' } }),
      'GET /api/category': () => ({ status: 401, body: { error: 'La sesión ya no es válida.' } }),
      'GET /api/report/monthly': () => ({ status: 401, body: { error: 'La sesión ya no es válida.' } }),
      'GET /api/transaction/paged': () => ({ status: 401, body: { error: 'La sesión ya no es válida.' } }),
    })

    renderWithProviders(<App />, { route: '/panel?mes=2026-09' })

    expect(await screen.findByText('Ingresá a tu cuenta')).toBeInTheDocument()
  })

  it('shows the sign-in form at /login', () => {
    renderWithProviders(<App />, { route: '/login' })

    expect(screen.getByText('Ingresá a tu cuenta')).toBeInTheDocument()
  })

  it('sends someone who is already signed in from /login to the dashboard', async () => {
    signedIn()
    mockDashboardApi()

    renderWithProviders(<App />, { route: '/login' })

    expect(await screen.findByRole('button', { name: 'Salir' })).toBeInTheDocument()
  })

  // The links of the mails are opened in whatever browser the user clicks them in, which may be one that is signed in
  it.each([
    ['/restablecer-contrasena?token=abc', 'Nueva contraseña'],
    ['/confirmar-correo?token=abc', 'Confirmar correo'],
  ])('shows %s even to someone who is already signed in, and not the dashboard', async (route, title) => {
    signedIn()
    mockDashboardApi()

    renderWithProviders(<App />, { route })

    expect(await screen.findByRole('heading', { name: title })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Salir' })).not.toBeInTheDocument()
  })

  it('shows the reset page to someone who has not signed in', () => {
    renderWithProviders(<App />, { route: '/restablecer-contrasena?token=abc' })

    expect(screen.getByRole('heading', { name: 'Nueva contraseña' })).toBeInTheDocument()
  })

  it('sends an address that does not exist to the root', () => {
    renderWithProviders(<App />, { route: '/no-existe' })

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Anotá lo que gastás.')
  })
})
