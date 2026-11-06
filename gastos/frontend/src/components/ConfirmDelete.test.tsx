import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import ConfirmDelete from './ConfirmDelete.tsx'

const openButton = () => screen.getByRole('button', { name: 'Eliminar algo' })

describe('ConfirmDelete', () => {
  it('shows only the cross at first, and does not delete when it is pressed', async () => {
    const onConfirm = vi.fn()
    render(<ConfirmDelete label="Eliminar algo" onConfirm={onConfirm} />)

    await userEvent.click(openButton())

    expect(onConfirm).not.toHaveBeenCalled()
    expect(screen.getByText('¿Eliminar?')).toBeInTheDocument()
  })

  it('deletes only after the user answers yes', async () => {
    const onConfirm = vi.fn()
    render(<ConfirmDelete label="Eliminar algo" onConfirm={onConfirm} />)

    await userEvent.click(openButton())
    await userEvent.click(screen.getByRole('button', { name: 'Sí' }))

    expect(onConfirm).toHaveBeenCalledTimes(1)
  })

  it('goes back to the cross without deleting when the user answers no', async () => {
    const onConfirm = vi.fn()
    render(<ConfirmDelete label="Eliminar algo" onConfirm={onConfirm} />)

    await userEvent.click(openButton())
    await userEvent.click(screen.getByRole('button', { name: 'No' }))

    expect(onConfirm).not.toHaveBeenCalled()
    expect(openButton()).toBeInTheDocument()
    expect(screen.queryByText('¿Eliminar?')).not.toBeInTheDocument()
  })

  it('cancels with Escape', async () => {
    const onConfirm = vi.fn()
    render(<ConfirmDelete label="Eliminar algo" onConfirm={onConfirm} />)

    await userEvent.click(openButton())
    await userEvent.keyboard('{Escape}')

    expect(onConfirm).not.toHaveBeenCalled()
    expect(openButton()).toBeInTheDocument()
  })

  it('puts the focus on "No", so pressing Enter by reflex cannot delete', async () => {
    render(<ConfirmDelete label="Eliminar algo" onConfirm={vi.fn()} />)

    await userEvent.click(openButton())

    expect(screen.getByRole('button', { name: 'No' })).toHaveFocus()
  })

  it('gives the focus back to the cross after cancelling', async () => {
    render(<ConfirmDelete label="Eliminar algo" onConfirm={vi.fn()} />)

    await userEvent.click(openButton())
    await userEvent.click(screen.getByRole('button', { name: 'No' }))

    expect(openButton()).toHaveFocus()
  })

  it('cannot be started or confirmed while a deletion is already in progress', async () => {
    const { rerender } = render(<ConfirmDelete label="Eliminar algo" onConfirm={vi.fn()} />)
    await userEvent.click(openButton())

    rerender(<ConfirmDelete label="Eliminar algo" onConfirm={vi.fn()} pending />)

    expect(screen.getByRole('button', { name: 'Sí' })).toBeDisabled()
  })

  it('disables the cross while pending', () => {
    render(<ConfirmDelete label="Eliminar algo" onConfirm={vi.fn()} pending />)

    expect(openButton()).toBeDisabled()
  })
})
