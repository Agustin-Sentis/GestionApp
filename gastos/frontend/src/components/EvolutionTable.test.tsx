import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { EvolutionPoint } from '../evolutionData.ts'
import { formatMoney } from '../format.ts'
import EvolutionTable from './EvolutionTable.tsx'

function point(key: string, label: string, income: number, expense: number, extra: Partial<EvolutionPoint> = {}): EvolutionPoint {
  return { key, label, income, expense, balance: income - expense, incomplete: false, unconverted: false, ...extra }
}

const totals = { income: 3000, expense: 1500, balance: 1500 }
const average = { income: 1500, expense: 750, balance: 750 }

/** The body and footer rows as an array of cell texts (the row header included). */
function rows() {
  return screen.getAllByRole('row').slice(1).map((row) => [...row.querySelectorAll('th, td')].map((cell) => cell.textContent))
}

describe('EvolutionTable', () => {
  it('has a row per month, with the month in words for whoever listens to it', () => {
    render(
      <EvolutionTable
        points={[point('2026-09', 'sep 26', 1000, 400), point('2026-10', 'oct 26', 2000, 1100)]}
        totals={totals}
        average={average}
      />,
    )

    expect(screen.getByRole('rowheader', { name: /septiembre de 2026/ })).toBeInTheDocument()
    expect(screen.getByRole('rowheader', { name: /octubre de 2026/ })).toBeInTheDocument()

    const body = rows()
    expect(body[0].slice(1)).toEqual([formatMoney(1000), formatMoney(400), formatMoney(600)])
    expect(body[1].slice(1)).toEqual([formatMoney(2000), formatMoney(1100), formatMoney(900)])
  })

  it('closes with the total and the monthly average', () => {
    render(<EvolutionTable points={[point('2026-09', 'sep 26', 3000, 1500)]} totals={totals} average={average} />)

    const body = rows()
    expect(body.at(-2)?.[0]).toBe('Total')
    expect(body.at(-2)?.slice(1)).toEqual([formatMoney(3000), formatMoney(1500), formatMoney(1500)])
    expect(body.at(-1)?.[0]).toBe('Promedio mensual')
    expect(body.at(-1)?.slice(1)).toEqual([formatMoney(1500), formatMoney(750), formatMoney(750)])
  })

  it('names the columns', () => {
    render(<EvolutionTable points={[point('2026-09', 'sep 26', 1, 0)]} totals={totals} average={average} />)

    expect(screen.getAllByRole('columnheader').map((h) => h.textContent)).toEqual(['Mes', 'Ingresos', 'Gastos', 'Saldo'])
  })

  it('writes "déficit" next to a negative balance, not only a color', () => {
    render(
      <EvolutionTable
        points={[point('2026-09', 'sep 26', 100, 400), point('2026-10', 'oct 26', 400, 100)]}
        totals={totals}
        average={average}
      />,
    )

    const [deficit, surplus] = screen.getAllByRole('row').slice(1, 3)
    expect(within(deficit).getByText('déficit')).toBeInTheDocument()
    expect(within(surplus).queryByText('déficit')).not.toBeInTheDocument()
  })

  it('marks the month in progress and the month that is incomplete for lack of a rate', () => {
    render(
      <EvolutionTable
        points={[
          point('2026-10', 'oct 26', 100, 50, { unconverted: true }),
          point('2026-11', 'nov 26', 100, 50, { incomplete: true }),
        ]}
        totals={totals}
        average={average}
      />,
    )

    const [october, november] = screen.getAllByRole('row').slice(1, 3)
    expect(within(october).getByText('incompleto')).toBeInTheDocument()
    expect(within(october).queryByText('(en curso)')).not.toBeInTheDocument()
    expect(within(november).getByText('(en curso)')).toBeInTheDocument()
    expect(within(november).queryByText('incompleto')).not.toBeInTheDocument()
  })
})
