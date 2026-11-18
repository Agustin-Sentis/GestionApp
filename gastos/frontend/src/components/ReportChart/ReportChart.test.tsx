import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ChartView } from '../../shared/chartData.ts'
import ReportChart from './ReportChart.tsx'

const donut = (center?: { value: string; label: string }): Exclude<ChartView, { shape: 'empty' }> => ({
  shape: 'donut',
  slices: [{ key: 'a', name: 'Comida', value: 100, percent: 100, color: 'var(--series-1)' }],
  percentLabel: '% de los gastos',
  note: null,
  center,
})

// The chart library has no size to draw in jsdom: what is checked is what this component puts around it
describe('ReportChart', () => {
  it('writes the figure that sums the donut up in its hole', () => {
    render(<ReportChart view={donut({ value: '38,4%', label: 'gastado' })} />)

    expect(screen.getByText('38,4%')).toBeInTheDocument()
    expect(screen.getByText('gastado')).toBeInTheDocument()
  })

  it('does not catch the mouse in the hole (the tooltip of the donut still works under it) and stays hidden from screen readers', () => {
    const { container } = render(<ReportChart view={donut({ value: '38,4%', label: 'gastado' })} />)

    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true')
    expect(screen.getByText('38,4%').parentElement).toHaveClass('pointer-events-none')
  })

  it('writes nothing in the hole when the view has no center, nor in a chart of bars', () => {
    const { rerender } = render(<ReportChart view={donut()} />)
    expect(screen.queryByText('gastado')).not.toBeInTheDocument()

    rerender(<ReportChart view={{ ...donut({ value: '1', label: 'gastado' }), shape: 'bars' }} />)
    expect(screen.queryByText('gastado')).not.toBeInTheDocument()
  })
})
