import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import SummaryModeToggle from './SummaryModeToggle.tsx'

describe('SummaryModeToggle', () => {
  it('is a labelled group with the current view pressed', () => {
    render(<SummaryModeToggle mode="mes" onChange={() => {}} />)

    const group = screen.getByRole('group', { name: 'Vista del resumen' })
    expect(group).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Este mes' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Evolución' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('presses the other one when the evolution is on', () => {
    render(<SummaryModeToggle mode="evolucion" onChange={() => {}} />)

    expect(screen.getByRole('button', { name: 'Este mes' })).toHaveAttribute('aria-pressed', 'false')
    expect(screen.getByRole('button', { name: 'Evolución' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('reports the view the user chose', async () => {
    const onChange = vi.fn()
    render(<SummaryModeToggle mode="mes" onChange={onChange} />)

    await userEvent.click(screen.getByRole('button', { name: 'Evolución' }))

    expect(onChange).toHaveBeenCalledWith('evolucion')
  })
})
