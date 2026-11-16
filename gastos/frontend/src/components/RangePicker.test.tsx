import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import RangePicker from './RangePicker.tsx'

function setup(mode: 'custom' | '3' | '6' | '12' | 'anio' = '6') {
  const onPreset = vi.fn()
  const onCustom = vi.fn()
  render(<RangePicker mode={mode} from="2026-06" to="2026-11" onPreset={onPreset} onCustom={onCustom} />)
  return { onPreset, onCustom }
}

describe('RangePicker', () => {
  it('shows the four shortcuts, with the one that is on pressed', () => {
    setup('12')

    expect(screen.getByRole('button', { name: 'Últimos 3 meses' })).toHaveAttribute('aria-pressed', 'false')
    expect(screen.getByRole('button', { name: 'Últimos 6 meses' })).toHaveAttribute('aria-pressed', 'false')
    expect(screen.getByRole('button', { name: 'Últimos 12 meses' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Este año' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('presses none of them when the range was written by hand', () => {
    setup('custom')

    for (const name of ['Últimos 3 meses', 'Últimos 6 meses', 'Últimos 12 meses', 'Este año']) {
      expect(screen.getByRole('button', { name })).toHaveAttribute('aria-pressed', 'false')
    }
  })

  it.each([
    ['Últimos 3 meses', '3'],
    ['Últimos 6 meses', '6'],
    ['Últimos 12 meses', '12'],
    ['Este año', 'anio'],
  ])('reports the shortcut %s as %s', async (name, preset) => {
    const { onPreset } = setup('6')

    await userEvent.click(screen.getByRole('button', { name }))

    expect(onPreset).toHaveBeenCalledWith(preset)
  })

  it('shows the range in use in two month fields, within the years the app accepts', () => {
    setup()

    const from = screen.getByLabelText('Desde')
    const to = screen.getByLabelText('Hasta')
    expect(from).toHaveValue('2026-06')
    expect(to).toHaveValue('2026-11')
    expect(from).toHaveAttribute('min', '2000-01')
    expect(to).toHaveAttribute('max', '2100-12')
  })

  it('reports the whole range when one end is changed', () => {
    const { onCustom } = setup()

    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-03' } })
    expect(onCustom).toHaveBeenLastCalledWith({ from: '2026-03', to: '2026-11' })

    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2026-09' } })
    expect(onCustom).toHaveBeenLastCalledWith({ from: '2026-06', to: '2026-09' })
  })

  it('ignores a field that was cleared', () => {
    const { onCustom } = setup()

    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '' } })

    expect(onCustom).not.toHaveBeenCalled()
  })
})
