import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { saveSession } from '../api.ts'
import type { MonthlyReport } from '../api.ts'
import { mockApi } from '../test/fetchMock.ts'
import { renderWithProviders } from '../test/render.tsx'
import MonthStats from './MonthStats.tsx'

function reportOf(overrides: Partial<MonthlyReport> = {}): MonthlyReport {
  return {
    year: 2026, month: 11, currency: 'ARS', income: 100000, expense: 70000, balance: 30000,
    byCategory: [], budgets: [], pendingConversions: [], hasUnconvertedUsd: false, previous: null, ...overrides,
  }
}

function setup(reply: () => { status?: number; body?: unknown }) {
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
  const fetchMock = mockApi({ 'GET /api/report/monthly': reply })
  renderWithProviders(<MonthStats year={2026} month={11} />)
  return fetchMock
}

/** The text of the figure under a label. */
const figureOf = (label: string) => screen.getByText(label).closest('div')?.querySelector('dd')?.textContent

describe('MonthStats', () => {
  it('shows what came in, what went out and what is left, in pesos', async () => {
    setup(() => ({ body: reportOf() }))

    expect(await screen.findByText('Ingresos')).toBeInTheDocument()
    expect(figureOf('Ingresos')).toMatch(/100\.000/)
    expect(figureOf('Gastos')).toMatch(/70\.000/)
    expect(figureOf('Te queda')).toMatch(/30\.000/)
  })

  it('leaves out the ",00" of whole pesos, so the figures fit in a narrow header', async () => {
    setup(() => ({ body: reportOf({ income: 2693000, expense: 809453, balance: 1883547 }) }))

    await screen.findByText('Ingresos')
    expect(figureOf('Ingresos')).toMatch(/2\.693\.000$/)
    expect(figureOf('Gastos')).toMatch(/809\.453$/)
    expect(figureOf('Te queda')).toMatch(/1\.883\.547$/)
  })

  it('keeps the cents when the amount has them: nothing is rounded away', async () => {
    setup(() => ({ body: reportOf({ income: 1000.5, expense: 400.25, balance: 600.25 }) }))

    await screen.findByText('Ingresos')
    expect(figureOf('Ingresos')).toMatch(/1\.000,50$/)
    expect(figureOf('Gastos')).toMatch(/400,25$/)
    expect(figureOf('Te queda')).toMatch(/600,25$/)
  })

  it('never lets a figure break in the middle, whatever the width: the figures wrap as a whole instead', async () => {
    setup(() => ({ body: reportOf({ income: 2693000, expense: 809453, balance: 1883547 }) }))

    await screen.findByText('Ingresos')
    const list = screen.getByText('Ingresos').closest('dl')!
    expect(list.className).toContain('flex-wrap')
    for (const label of ['Ingresos', 'Gastos', 'Te queda']) {
      expect(screen.getByText(label).closest('div')?.querySelector('dd span')?.className).toContain('whitespace-nowrap')
    }
  })

  it('says "Te pasaste" with the amount, without a minus sign, when the expenses are above the income', async () => {
    setup(() => ({ body: reportOf({ income: 50000, expense: 80000, balance: -30000 }) }))

    expect(await screen.findByText('Te pasaste')).toBeInTheDocument()
    expect(screen.queryByText('Te queda')).not.toBeInTheDocument()
    expect(figureOf('Te pasaste')).toMatch(/30\.000/)
    expect(figureOf('Te pasaste')).not.toMatch(/-|−/)
  })

  it('says "Te queda" with zero when the month is exactly even', async () => {
    setup(() => ({ body: reportOf({ income: 100, expense: 100, balance: 0 }) }))

    expect(await screen.findByText('Te queda')).toBeInTheDocument()
  })

  it('tells screen readers it is loading, then shows the figures', async () => {
    setup(() => ({ body: reportOf() }))

    expect(screen.getByText('Cargando las cifras del mes…')).toBeInTheDocument()
    expect(await screen.findByText('Gastos')).toBeInTheDocument()
    expect(screen.queryByText('Cargando las cifras del mes…')).not.toBeInTheDocument()
  })

  describe('comparison with the month before', () => {
    const october = { year: 2026, month: 10, income: 80000, expense: 100000, balance: -20000, hasUnconvertedUsd: false }

    /** The small line under a figure, as it is drawn (the arrow and the amount), or undefined when there is none. */
    const comparisonOf = (label: string) => {
      const extra = screen.getByText(label).closest('div')?.querySelectorAll('dd')[1]
      return extra?.querySelector('[aria-hidden="true"]')?.textContent
    }

    it('says how each figure changed since the month before, naming it', async () => {
      // November: 100.000 in (25% more than October), 70.000 out (30% less), 30.000 left (50.000 better than October's -20.000)
      setup(() => ({ body: reportOf({ previous: october }) }))

      await screen.findByText('Ingresos')
      expect(comparisonOf('Ingresos')).toBe('▲ 25% vs. octubre')
      expect(comparisonOf('Gastos')).toBe('▼ 30% vs. octubre')
      expect(comparisonOf('Te queda')).toMatch(/^▲ \$\s?50\.000 vs\. octubre$/)
    })

    it('gives screen readers the whole sentence instead of the arrows', async () => {
      setup(() => ({ body: reportOf({ previous: october }) }))

      expect(await screen.findByText('25% más que en octubre')).toBeInTheDocument()
      expect(screen.getByText('30% menos que en octubre')).toBeInTheDocument()
      expect(screen.getByText(/50\.000 más que en octubre/)).toBeInTheDocument()
    })

    it('hides the month on a phone, where there is no room, but keeps the arrow and the amount', async () => {
      setup(() => ({ body: reportOf({ previous: october }) }))

      await screen.findByText('Ingresos')
      const extra = screen.getByText('Ingresos').closest('div')?.querySelectorAll('dd')[1]
      const month = extra?.querySelector('[aria-hidden="true"] span')
      expect(month?.className).toContain('hidden')
      expect(month?.className).toContain('sm:inline')
    })

    it('explains the change in the tooltip too', async () => {
      setup(() => ({ body: reportOf({ previous: october }) }))

      await screen.findByText('Ingresos')
      const extra = screen.getByText('Ingresos').closest('div')?.querySelectorAll('dd')[1]
      expect(extra?.querySelector('[aria-hidden="true"]')).toHaveAttribute('title', '25% más que en octubre')
    })

    it('shows nothing extra when there is no previous month', async () => {
      setup(() => ({ body: reportOf({ previous: null }) }))

      await screen.findByText('Ingresos')
      expect(comparisonOf('Ingresos')).toBeUndefined()
      expect(screen.queryByText(/más que en|menos que en|Igual que en/)).not.toBeInTheDocument()
    })

    it('shows nothing when the month before had no movements: there is nothing to compare against', async () => {
      setup(() => ({ body: reportOf({ previous: { ...october, income: 0, expense: 0, balance: 0 } }) }))

      await screen.findByText('Ingresos')
      expect(screen.queryByText(/más que en|menos que en|Igual que en/)).not.toBeInTheDocument()
    })

    it('shows nothing when the month before is incomplete for lack of a USD rate', async () => {
      setup(() => ({ body: reportOf({ previous: { ...october, hasUnconvertedUsd: true } }) }))

      await screen.findByText('Ingresos')
      expect(screen.queryByText(/más que en|menos que en|Igual que en/)).not.toBeInTheDocument()
    })

    it('skips only the percentage of a figure that was zero before', async () => {
      setup(() => ({ body: reportOf({ previous: { ...october, income: 0 } }) }))

      await screen.findByText('Ingresos')
      expect(comparisonOf('Ingresos')).toBeUndefined() // no income last month: no percentage
      expect(comparisonOf('Gastos')).toBe('▼ 30% vs. octubre')
    })

    it('compares a month that went over the income too', async () => {
      setup(() => ({
        body: reportOf({ income: 50000, expense: 80000, balance: -30000, previous: { ...october, balance: 10000 } }),
      }))

      expect(await screen.findByText('Te pasaste')).toBeInTheDocument()
      expect(comparisonOf('Te pasaste')).toMatch(/^▼ \$\s?40\.000 vs\. octubre$/)
    })

    it('names December when the month shown is January', async () => {
      setup(() => ({ body: reportOf({ previous: { ...october, year: 2025, month: 12 } }) }))

      expect(await screen.findByText('25% más que en diciembre')).toBeInTheDocument()
    })
  })

  it('shows nothing when the report fails: the summary panel is the one that explains it', async () => {
    const fetchMock = setup(() => ({ status: 500, body: { error: 'caído' } }))

    await waitForTheAnswer(fetchMock)
    expect(screen.queryByText('Ingresos')).not.toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
})

/** Waits until the request was made and answered, so "nothing is shown" is checked after the failure and not before. */
async function waitForTheAnswer(fetchMock: ReturnType<typeof mockApi>) {
  await screen.findByText('Cargando las cifras del mes…').catch(() => undefined)
  await new Promise((resolve) => setTimeout(resolve, 50))
  expect(fetchMock).toHaveBeenCalled()
}
