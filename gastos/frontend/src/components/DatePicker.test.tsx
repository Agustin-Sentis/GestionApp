import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { todayLocal } from '../format.ts'
import { dateButton, pickDate } from '../test/datePicker.ts'
import DatePicker from './DatePicker.tsx'

function Harness({ initial = '2026-11-17', clearable = false, onChange }: { initial?: string; clearable?: boolean; onChange?: (value: string) => void }) {
  const [value, setValue] = useState(initial)
  return (
    <>
      <DatePicker
        label="Fecha"
        value={value}
        clearable={clearable}
        onChange={(next) => {
          setValue(next)
          onChange?.(next)
        }}
      />
      <button type="button">otro botón</button>
      <output aria-label="valor">{value}</output>
    </>
  )
}

const value = () => screen.getByLabelText('valor').textContent

describe('DatePicker', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] }) // only the clock: the typing and the clicks of the test keep their own time
    vi.setSystemTime(new Date(2026, 10, 17, 12))
  })
  afterEach(() => vi.useRealTimers())

  it('shows the date as dd/mm/aaaa, and a hint when there is none', () => {
    const { unmount } = render(<Harness />)
    expect(dateButton('Fecha')).toHaveTextContent('17/11/2026')
    unmount()

    render(<Harness initial="" clearable />)
    expect(dateButton('Fecha')).toHaveTextContent('dd/mm/aaaa')
  })

  it('is closed until it is pressed, and opens on the month of the date with it marked', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness initial="2026-03-05" />)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(dateButton('Fecha')).toHaveAttribute('aria-expanded', 'false')

    await user.click(dateButton('Fecha'))

    const calendar = screen.getByRole('dialog', { name: 'Fecha' })
    expect(dateButton('Fecha')).toHaveAttribute('aria-expanded', 'true')
    expect(within(calendar).getByRole('button', { name: /5 de marzo de 2026, seleccionado/ })).toBeInTheDocument()
  })

  it('chooses a day, closes, and gives the focus back to the field', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const onChange = vi.fn()
    render(<Harness onChange={onChange} />)

    await pickDate(user, 'Fecha', '2026-12-03')

    expect(onChange).toHaveBeenCalledWith('2026-12-03')
    expect(value()).toBe('2026-12-03')
    expect(dateButton('Fecha')).toHaveTextContent('03/12/2026')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(dateButton('Fecha')).toHaveFocus()
  })

  it('can go to a very old day: the year is chosen, it is not paged one month at a time', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness />)

    await pickDate(user, 'Fecha', '2003-01-31')

    expect(value()).toBe('2003-01-31')
  })

  it('"Hoy" chooses today', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness initial="2026-03-05" />)

    await user.click(dateButton('Fecha'))
    await user.click(screen.getByRole('button', { name: 'Hoy' }))

    expect(value()).toBe(todayLocal())
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('offers "Borrar" only when it can be empty, and empties it', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const { unmount } = render(<Harness />)
    await user.click(dateButton('Fecha'))
    expect(screen.queryByRole('button', { name: 'Borrar' })).not.toBeInTheDocument()
    unmount()

    render(<Harness clearable />)
    await user.click(dateButton('Fecha'))
    await user.click(screen.getByRole('button', { name: 'Borrar' }))

    expect(value()).toBe('')
    expect(dateButton('Fecha')).toHaveTextContent('dd/mm/aaaa')
  })

  it('pressing the chosen day again empties a field that can be empty, and keeps a date that must exist', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const { unmount } = render(<Harness clearable />)
    await user.click(dateButton('Fecha'))
    await user.click(screen.getByRole('button', { name: /17 de noviembre de 2026/ }))
    expect(value()).toBe('')
    unmount()

    render(<Harness />)
    await user.click(dateButton('Fecha'))
    await user.click(screen.getByRole('button', { name: /17 de noviembre de 2026/ }))
    expect(value()).toBe('2026-11-17')
  })

  it('"Borrar" is off while there is no date to clear', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness initial="" clearable />)

    await user.click(dateButton('Fecha'))

    expect(screen.getByRole('button', { name: 'Borrar' })).toBeDisabled()
  })

  it('closes with Esc (and returns the focus), changing nothing', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness />)
    await user.click(dateButton('Fecha'))

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(dateButton('Fecha')).toHaveFocus()
    expect(value()).toBe('2026-11-17')
  })

  it('closes with a click outside, and when the focus moves out of it with Tab', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness />)

    await user.click(dateButton('Fecha'))
    await user.click(screen.getByText('otro botón'))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await user.click(dateButton('Fecha'))
    act(() => screen.getByText('otro botón').focus())
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('moves between the days with the arrow keys and chooses with Enter', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness />)
    await user.click(dateButton('Fecha'))

    // The focus starts on the chosen day (the 17th); two to the right is the 19th
    await user.keyboard('{ArrowRight}{ArrowRight}{Enter}')

    expect(value()).toBe('2026-11-19')
  })

  it('closes when it is pressed again', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    render(<Harness />)

    await user.click(dateButton('Fecha'))
    await user.click(dateButton('Fecha'))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
