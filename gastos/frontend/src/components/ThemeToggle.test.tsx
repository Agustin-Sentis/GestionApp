import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ThemeToggle from './ThemeToggle.tsx'

afterEach(() => {
  document.documentElement.classList.remove('dark')
  document.documentElement.style.colorScheme = ''
})

describe('ThemeToggle', () => {
  it('starts on "Sistema" when nothing was chosen', () => {
    render(<ThemeToggle />)

    expect(screen.getByLabelText('Tema')).toHaveValue('system')
  })

  it('starts on the theme that was saved, and applies it', () => {
    localStorage.setItem('gastos.theme', 'dark')

    render(<ThemeToggle />)

    expect(screen.getByLabelText('Tema')).toHaveValue('dark')
    expect(document.documentElement).toHaveClass('dark')
  })

  it('switches the page to dark and light as the user picks them, and remembers the choice', async () => {
    render(<ThemeToggle />)

    await userEvent.selectOptions(screen.getByLabelText('Tema'), 'dark')
    expect(document.documentElement).toHaveClass('dark')
    expect(localStorage.getItem('gastos.theme')).toBe('dark')

    await userEvent.selectOptions(screen.getByLabelText('Tema'), 'light')
    expect(document.documentElement).not.toHaveClass('dark')
    expect(localStorage.getItem('gastos.theme')).toBe('light')
  })

  it('forgets the saved choice when the user goes back to "Sistema"', async () => {
    localStorage.setItem('gastos.theme', 'dark')
    render(<ThemeToggle />)

    await userEvent.selectOptions(screen.getByLabelText('Tema'), 'system')

    expect(localStorage.getItem('gastos.theme')).toBeNull()
  })

  it('follows the system while on "Sistema", including when it changes', async () => {
    let dark = false
    const listeners = new Set<() => void>()
    vi.stubGlobal('matchMedia', () => ({
      get matches() {
        return dark
      },
      addEventListener: (_type: string, listener: () => void) => listeners.add(listener),
      removeEventListener: (_type: string, listener: () => void) => listeners.delete(listener),
    }))
    const { unmount } = render(<ThemeToggle />)
    expect(document.documentElement).not.toHaveClass('dark')

    dark = true
    listeners.forEach((listener) => listener()) // the operating system switched to dark

    expect(document.documentElement).toHaveClass('dark')

    unmount()
    expect(listeners.size).toBe(0) // it stopped listening
  })

  it('ignores the system once light or dark is forced', async () => {
    const listeners = new Set<() => void>()
    vi.stubGlobal('matchMedia', () => ({
      matches: true,
      addEventListener: (_type: string, listener: () => void) => listeners.add(listener),
      removeEventListener: (_type: string, listener: () => void) => listeners.delete(listener),
    }))
    render(<ThemeToggle />)

    await userEvent.selectOptions(screen.getByLabelText('Tema'), 'light')

    expect(document.documentElement).not.toHaveClass('dark') // the system says dark, the user said light
    expect(listeners.size).toBe(0)
  })
})
