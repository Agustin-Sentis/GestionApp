import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import type { BudgetProgress, Category, MonthlyReport } from '../api.ts'
import { currentMonthValue, formatMoneyCompact, parseMonthValue } from '../format.ts'
import { mockApi } from '../test/fetchMock.ts'
import type { MockReply } from '../test/fetchMock.ts'
import { food, salary, settings } from '../test/fixtures.ts'
import { renderWithProviders } from '../test/render.tsx'
import BudgetPanel from './BudgetPanel.tsx'

const rent: Category = { id: 'a', name: 'Alquiler', type: 'Expense', monthlyBudget: 100000 }

function progress(overrides: Partial<BudgetProgress> = {}): BudgetProgress {
  return { categoryId: rent.id, name: 'Alquiler', monthlyBudget: 100000, spent: 50000, usedPercent: 50, status: 'Ok', ...overrides }
}

function reportOf(budgets: BudgetProgress[], overrides: Partial<MonthlyReport> = {}): MonthlyReport {
  return {
    year: 2026, month: 11, currency: 'ARS', income: 0, expense: 0, balance: 0,
    byCategory: [], budgets, pendingConversions: [], hasUnconvertedUsd: false, previous: null,
    ...overrides,
  }
}

interface Setup {
  report?: MonthlyReport
  categories?: Category[]
  /** What the server answers to saving a budget. */
  saveReply?: MockReply
  year?: number
  month?: number
}

/** Renders the panel and returns the bodies of the PUTs it sent (parsed). */
function setup({ report = reportOf([progress()]), categories = [food, rent, salary], saveReply, year = 2026, month = 11 }: Setup = {}) {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  const saved: unknown[] = []
  const save = (init: RequestInit): MockReply => {
    saved.push(JSON.parse(String(init.body)))
    return saveReply ?? { body: {} }
  }
  mockApi({
    'GET /api/category': () => ({ body: categories }),
    'GET /api/report/monthly': () => ({ body: report }),
    'GET /api/settings': () => ({ body: settings() }),
    [`PUT /api/category/${food.id}/budget`]: save,
    [`PUT /api/category/${rent.id}/budget`]: save,
  })
  renderWithProviders(<BudgetPanel year={year} month={month} />)
  return saved
}

beforeEach(() => localStorage.clear())

describe('BudgetPanel', () => {
  it('shows each budget with what was spent, the bar and the state in words, the most urgent first', async () => {
    setup({
      report: reportOf([
        progress({ categoryId: 'x1', name: 'Comida', monthlyBudget: 100000, spent: 120000, usedPercent: 120, status: 'Exceeded' }),
        progress({ categoryId: 'x2', name: 'Gym', monthlyBudget: 10000, spent: 8300, usedPercent: 83, status: 'Warning' }),
        progress({ categoryId: 'x3', name: 'Alquiler', monthlyBudget: 100000, spent: 50000, usedPercent: 50, status: 'Ok' }),
      ]),
    })

    const bars = await screen.findAllByRole('progressbar')
    expect(bars.map((bar) => bar.getAttribute('aria-label'))).toEqual([
      'Presupuesto de Comida',
      'Presupuesto de Gym',
      'Presupuesto de Alquiler',
    ])
    // Over the budget: the bar is full, and the words say by how much
    expect(bars[0]).toHaveAttribute('aria-valuenow', '100')
    expect(bars[0]).toHaveAttribute('aria-valuetext', `${formatMoneyCompact(120000)} de ${formatMoneyCompact(100000)}`)
    // The text of the page has its spaces normalized (the peso format uses a non-breaking one); the expected one has to match
    expect(screen.getByText(`Superado por ${formatMoneyCompact(20000)}`.replace(/\s+/g, ' '))).toBeInTheDocument()
    expect(screen.getByText('Cerca del límite (83%)')).toBeInTheDocument()
    expect(bars[1]).toHaveAttribute('aria-valuenow', '83')
    expect(screen.getByText('50%')).toBeInTheDocument()
  })

  it('invites to create the first budget when there is none', async () => {
    setup({ report: reportOf([]), categories: [{ ...rent, monthlyBudget: null }, salary] })

    expect(await screen.findByText(/Todavía no definiste ningún presupuesto/)).toBeInTheDocument()
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
  })

  it('asks for an expense category first when there is none', async () => {
    setup({ report: reportOf([]), categories: [salary] })

    expect(await screen.findByText(/Creá una categoría de gasto/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Agregar' })).not.toBeInTheDocument()
  })

  it('offers only the expense categories that have no budget yet', async () => {
    setup()

    const select = await screen.findByLabelText('Categoría')
    const options = within(select).getAllByRole('option').map((o) => o.textContent)
    expect(options).toEqual(['Elegí…', 'Comida'])
  })

  it('adds a budget to a category', async () => {
    const user = userEvent.setup()
    const saved = setup()

    await user.selectOptions(await screen.findByLabelText('Categoría'), 'Comida')
    await user.type(screen.getByLabelText('Monto por mes'), '50000')
    await user.click(screen.getByRole('button', { name: 'Agregar' }))

    expect(await screen.findByText('Presupuesto guardado')).toBeInTheDocument()
    expect(saved).toEqual([{ monthlyBudget: 50000 }])
  })

  it('edits the amount of a budget, starting from the current one', async () => {
    const user = userEvent.setup()
    const saved = setup()

    await user.click(await screen.findByRole('button', { name: 'Editar el presupuesto de Alquiler' }))
    const field = screen.getByLabelText('Presupuesto mensual')
    expect(field).toHaveValue(100000)
    await user.clear(field)
    await user.type(field, '120000')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Presupuesto guardado')).toBeInTheDocument()
    expect(saved).toEqual([{ monthlyBudget: 120000 }])
    // The form closes once it is saved
    await waitFor(() => expect(screen.queryByLabelText('Presupuesto mensual')).not.toBeInTheDocument())
  })

  it('cancels an edit without saving anything', async () => {
    const user = userEvent.setup()
    const saved = setup()

    await user.click(await screen.findByRole('button', { name: 'Editar el presupuesto de Alquiler' }))
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByLabelText('Presupuesto mensual')).not.toBeInTheDocument()
    expect(saved).toEqual([])
  })

  it('removes a budget', async () => {
    const user = userEvent.setup()
    const saved = setup()

    await user.click(await screen.findByRole('button', { name: 'Editar el presupuesto de Alquiler' }))
    await user.click(screen.getByRole('button', { name: 'Quitar presupuesto' }))

    expect(await screen.findByText('Presupuesto quitado')).toBeInTheDocument()
    expect(saved).toEqual([{ monthlyBudget: null }])
  })

  it('shows what the server says when it refuses the amount, and keeps the form open', async () => {
    const user = userEvent.setup()
    setup({ saveReply: { status: 400, body: { error: 'El presupuesto debe ser mayor a cero.' } } })

    await user.click(await screen.findByRole('button', { name: 'Editar el presupuesto de Alquiler' }))
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('El presupuesto debe ser mayor a cero.')
    expect(screen.getByLabelText('Presupuesto mensual')).toBeInTheDocument()
  })

  it('says that a past month is measured with the current budget', async () => {
    setup({ year: 2020, month: 3 })
    expect(await screen.findByText(/Un mes pasado se mide con el presupuesto que tiene hoy/)).toBeInTheDocument()
  })

  it('does not say it for the current month', async () => {
    const { year, month } = parseMonthValue(currentMonthValue())
    setup({ year, month })

    await screen.findByRole('progressbar')
    expect(screen.queryByText(/Un mes pasado/)).not.toBeInTheDocument()
  })

  it('warns when some USD movements could not be added to what was spent', async () => {
    setup({ report: reportOf([progress()], { hasUnconvertedUsd: true }) })

    expect(await screen.findByText(/lo gastado puede ser más/)).toBeInTheDocument()
  })

  it('has the switch for the alert mails at the foot of the card', async () => {
    setup()

    const card = (await screen.findByRole('heading', { name: 'Presupuestos' })).closest('section')!
    const box = await within(card).findByRole('checkbox', { name: /Avisarme por correo/ })
    expect(box).not.toBeChecked()
  })

  it('shows nothing when the report could not be loaded: the summary above explains it', async () => {
    saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
    mockApi({
      'GET /api/category': () => ({ body: [food] }),
      'GET /api/report/monthly': () => ({ status: 500, body: { error: 'boom' } }),
    })
    renderWithProviders(<BudgetPanel year={2026} month={11} />)

    // While it loads the card shows its title with placeholders; once the report fails the whole card goes away
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Presupuestos' })).not.toBeInTheDocument())
  })
})
