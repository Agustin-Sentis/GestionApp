import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { EvolutionPoint } from '../evolutionData.ts'
import EvolutionChart from './EvolutionChart.tsx'

const points: EvolutionPoint[] = [
  { key: '2026-09', label: 'sep 26', income: 1000, expense: 400, balance: 600, incomplete: false, unconverted: false },
  { key: '2026-10', label: 'oct 26', income: 500, expense: 900, balance: -400, incomplete: true, unconverted: false },
]

// The chart library has no size to draw in jsdom: what is checked is what this component puts around it
describe('EvolutionChart', () => {
  it('is hidden from screen readers: the table next to it says every figure', () => {
    const { container } = render(<EvolutionChart points={points} />)

    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true')
  })

  it('has a key that says which mark is which', () => {
    render(<EvolutionChart points={points} />)

    expect(screen.getByText('Ingresos')).toBeInTheDocument()
    expect(screen.getByText('Gastos')).toBeInTheDocument()
    expect(screen.getByText('Saldo')).toBeInTheDocument()
  })
})
