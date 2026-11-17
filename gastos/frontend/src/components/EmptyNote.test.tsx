import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import EmptyNote from './EmptyNote.tsx'

describe('EmptyNote', () => {
  it('says what is missing, written by hand', () => {
    render(<EmptyNote>Todavía no agregaste gastos este mes.</EmptyNote>)

    expect(screen.getByText('Todavía no agregaste gastos este mes.')).toHaveClass('hand')
  })

  it('offers the next step as a button, and presses it', async () => {
    const onAction = vi.fn()
    render(<EmptyNote actionLabel="Agregar un movimiento" onAction={onAction}>Nada todavía.</EmptyNote>)

    await userEvent.click(screen.getByRole('button', { name: 'Agregar un movimiento' }))

    expect(onAction).toHaveBeenCalledOnce()
  })

  it('has no button when there is no action to take (a label alone would do nothing)', () => {
    const { rerender } = render(<EmptyNote actionLabel="Agregar un movimiento">Nada todavía.</EmptyNote>)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()

    rerender(<EmptyNote onAction={() => {}}>Nada todavía.</EmptyNote>)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
