import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ThemeToggle from './ThemeToggle.tsx'

afterEach(() => {
  document.documentElement.classList.remove('dark')
  document.documentElement.style.colorScheme = ''
})

const choice = (name: RegExp | string) => screen.getByRole('radio', { name })

describe('ThemeToggle', () => {
  it('shows the three choices at once, as a group called "Tema"', () => {
    render(<ThemeToggle />)

    const group = screen.getByRole('group', { name: 'Tema' })
    expect(group).toBeInTheDocument()
    expect(screen.getAllByRole('radio').map((radio) => radio.getAttribute('value'))).toEqual(['system', 'light', 'dark'])
    expect(choice('Sistema')).toBeInTheDocument()
    expect(choice(/^Hoja/)).toBeInTheDocument()
    expect(choice(/^Pizarrón/)).toBeInTheDocument()
  })

  it('starts on "Sistema" when nothing was chosen', () => {
    render(<ThemeToggle />)

    expect(choice('Sistema')).toBeChecked()
    expect(choice(/^Pizarrón/)).not.toBeChecked()
  })

  it('starts on the theme that was saved, and applies it', () => {
    localStorage.setItem('gastos.theme', 'dark')

    render(<ThemeToggle />)

    expect(choice(/^Pizarrón/)).toBeChecked()
    expect(document.documentElement).toHaveClass('dark')
  })

  it('switches the page to the chalkboard and back to the page, and remembers the choice', async () => {
    render(<ThemeToggle />)

    await userEvent.click(choice(/^Pizarrón/))
    expect(document.documentElement).toHaveClass('dark')
    expect(localStorage.getItem('gastos.theme')).toBe('dark')
    expect(choice(/^Pizarrón/)).toBeChecked()

    await userEvent.click(choice(/^Hoja/))
    expect(document.documentElement).not.toHaveClass('dark')
    expect(localStorage.getItem('gastos.theme')).toBe('light')
    expect(choice(/^Hoja/)).toBeChecked()
  })

  it('forgets the saved choice when the user goes back to "Sistema"', async () => {
    localStorage.setItem('gastos.theme', 'dark')
    render(<ThemeToggle />)

    await userEvent.click(choice('Sistema'))

    expect(localStorage.getItem('gastos.theme')).toBeNull()
  })

  it('moves between the choices with the arrow keys', async () => {
    render(<ThemeToggle />)

    choice('Sistema').focus()
    await userEvent.keyboard('{ArrowRight}')

    expect(choice(/^Hoja/)).toBeChecked()
    expect(localStorage.getItem('gastos.theme')).toBe('light')
  })

  it('explains each drawing in a note that is also its description for screen readers', () => {
    render(<ThemeToggle />)

    expect(screen.getAllByRole('tooltip')).toHaveLength(3)
    expect(choice('Sistema')).toHaveAccessibleDescription(/Sistema.*dispositivo/)
    expect(choice(/^Hoja/)).toHaveAccessibleDescription(/Hoja.*hoja cuadriculada/)
    expect(choice(/^Pizarrón/)).toHaveAccessibleDescription(/Pizarrón.*pizarrón con tiza/)
  })

  it('keeps the note out of the name of the choice', () => {
    render(<ThemeToggle />)

    // If the note were inside the label, the name would be "Sistema Sistema Usa el modo…" and this would not match exactly
    expect(choice('Sistema')).toBeInTheDocument()
    expect(choice('Hoja (claro)')).toBeInTheDocument()
    expect(choice('Pizarrón (oscuro)')).toBeInTheDocument()
  })

  it('does not use the browser\'s own tooltip as well, which would show two at once', () => {
    render(<ThemeToggle />)

    for (const radio of screen.getAllByRole('radio')) {
      expect(radio.closest('label')).not.toHaveAttribute('title')
    }
  })

  it('has the note hidden until the choice is hovered or focused, and not clipped by the control', () => {
    render(<ThemeToggle />)

    const note = screen.getAllByRole('tooltip')[0]
    expect(note.className).toContain('invisible')
    expect(note.className).toContain('group-hover:visible')
    expect(note.className).toContain('group-has-focus-visible:visible')
    // The frame must not clip it: overflow would cut the note off where it hangs below the control
    expect(screen.getByRole('group', { name: 'Tema' }).className).not.toContain('overflow')
  })

  it('follows the system while on "Sistema", including when it changes', () => {
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

  it('ignores the system once the page or the chalkboard is forced', async () => {
    const listeners = new Set<() => void>()
    vi.stubGlobal('matchMedia', () => ({
      matches: true,
      addEventListener: (_type: string, listener: () => void) => listeners.add(listener),
      removeEventListener: (_type: string, listener: () => void) => listeners.delete(listener),
    }))
    render(<ThemeToggle />)

    await userEvent.click(choice(/^Hoja/))

    expect(document.documentElement).not.toHaveClass('dark') // the system says dark, the user said page
    expect(listeners.size).toBe(0)
  })
})
