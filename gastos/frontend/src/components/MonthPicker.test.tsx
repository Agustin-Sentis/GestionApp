import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { dateButton, pickMonth } from '../test/datePicker.ts'
import MonthPicker from './MonthPicker.tsx'

function Harness({ initial = '2026-11' }: { initial?: string }) {
  const [value, setValue] = useState(initial)
  return (
    <>
      <MonthPicker label="Mes" value={value} onChange={setValue} />
      <button type="button">otro botón</button>
      <output aria-label="valor">{value}</output>
    </>
  )
}

const value = () => screen.getByLabelText('valor').textContent

describe('MonthPicker', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 10, 17, 12))
  })
  afterEach(() => vi.useRealTimers())

  it('shows the month written out, and opens on its year with it marked', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness initial="2025-03" />)
    expect(dateButton('Mes')).toHaveTextContent('marzo de 2025')

    await user.click(dateButton('Mes'))

    const picker = screen.getByRole('dialog', { name: 'Mes' })
    expect(within(picker).getByText('2025')).toBeInTheDocument()
    expect(within(picker).getByRole('button', { name: 'marzo de 2025' })).toHaveAttribute('aria-pressed', 'true')
    expect(within(picker).getByRole('button', { name: 'abril de 2025' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('chooses a month, closes, and gives the focus back to the field', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness />)

    await pickMonth(user, 'Mes', '2027-01')

    expect(value()).toBe('2027-01')
    expect(dateButton('Mes')).toHaveTextContent('enero de 2027')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(dateButton('Mes')).toHaveFocus()
  })

  it('moving the year does not choose anything', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness />)
    await user.click(dateButton('Mes'))

    await user.click(screen.getByRole('button', { name: 'Año anterior' }))

    expect(screen.getByText('2025')).toBeInTheDocument()
    expect(value()).toBe('2026-11')
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('does not go past the years the app accepts', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness initial="2000-05" />)

    await user.click(dateButton('Mes'))
    expect(screen.getByRole('button', { name: 'Año anterior' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Año siguiente' }))
    expect(screen.getByRole('button', { name: 'Año anterior' })).toBeEnabled()
  })

  it('"Este mes" chooses the current one', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness initial="2024-02" />)

    await user.click(dateButton('Mes'))
    await user.click(screen.getByRole('button', { name: 'Este mes' }))

    expect(value()).toBe('2026-11')
  })

  it('closes with Esc and with a click outside, changing nothing', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness />)

    await user.click(dateButton('Mes'))
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(dateButton('Mes')).toHaveFocus()

    await user.click(dateButton('Mes'))
    await user.click(screen.getByText('otro botón'))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await user.click(dateButton('Mes'))
    act(() => screen.getByText('otro botón').focus())
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(value()).toBe('2026-11')
  })
})
