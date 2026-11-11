import { render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import { mockApi, mockFetch } from '../test/fetchMock.ts'
import { pageOf } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import CategoryManager from './CategoryManager.tsx'
import MonthlyReportView from './MonthlyReportView.tsx'
import { Skeleton, SkeletonGroup } from './Skeleton.tsx'
import TransactionList from './TransactionList.tsx'

function signedIn() {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
}

/** A response the test releases by hand, so it can look at the screen while the request is still pending. */
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((r) => { resolve = r })
  return { promise, resolve }
}

describe('Skeleton', () => {
  it('is hidden from screen readers', () => {
    const { container } = render(<Skeleton className="h-4" />)

    expect(container.firstChild).toHaveAttribute('aria-hidden', 'true')
  })

  it('only pulses when the user has not asked for less motion', () => {
    const { container } = render(<Skeleton />)

    expect(container.firstChild).toHaveClass('motion-safe:animate-pulse')
    expect(container.firstChild).not.toHaveClass('animate-pulse')
  })

  it('tells screen readers what is loading once, and marks the section as busy', () => {
    render(
      <SkeletonGroup label="Cargando algo…">
        <Skeleton />
        <Skeleton />
      </SkeletonGroup>,
    )

    const group = screen.getByRole('status')
    expect(group).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByText('Cargando algo…')).toBeInTheDocument()
  })
})

describe('loading states', () => {
  it('shows placeholders for the report, then the real figures', async () => {
    signedIn()
    const response = deferred<object>()
    mockApi({
      'GET /api/category': () => ({ body: [] }),
      'GET /api/report/monthly': async () => ({ body: await response.promise }),
    })
    renderWithProviders(<MonthlyReportView year={2026} month={11} />)

    expect(screen.getByText('Cargando el resumen…')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Resumen del mes' })).toBeInTheDocument() // the title does not jump

    response.resolve({
      year: 2026, month: 11, currency: 'ARS', income: 1000, expense: 400, balance: 600,
      budgets: [],
      byCategory: [
        { name: 'Sueldo', type: 'Income', total: 1000 },
        { name: 'Comida', type: 'Expense', total: 400 },
      ],
      pendingConversions: [], hasUnconvertedUsd: false,
    })

    await waitFor(() => expect(screen.queryByText('Cargando el resumen…')).not.toBeInTheDocument())
    expect(screen.getByText('Sin gastar')).toBeInTheDocument()
  })

  it('shows placeholders for the transactions, then the list', async () => {
    signedIn()
    const response = deferred<object>()
    mockApi({
      'GET /api/category': () => ({ body: [] }),
      // The test holds this one back to look at the screen while the list is still loading
      'GET /api/transaction/paged': async () => ({ body: await response.promise }),
    })
    renderWithProviders(<TransactionList />)

    expect(screen.getByText('Cargando los movimientos…')).toBeInTheDocument()

    response.resolve(pageOf([]))

    expect(await screen.findByText('No hay movimientos en este mes.')).toBeInTheDocument()
    expect(screen.queryByText('Cargando los movimientos…')).not.toBeInTheDocument()
  })

  it('shows placeholders for the categories, and not the empty-state text while loading', async () => {
    signedIn()
    const response = deferred<object[]>()
    mockFetch(async () => ({ body: await response.promise }))
    renderWithProviders(<CategoryManager />)

    expect(screen.getByText('Cargando las categorías…')).toBeInTheDocument()
    expect(screen.queryByText('Todavía no tenés categorías.')).not.toBeInTheDocument()

    response.resolve([])

    expect(await screen.findByText('Todavía no tenés categorías.')).toBeInTheDocument()
    expect(screen.queryByText('Cargando las categorías…')).not.toBeInTheDocument()
  })
})
