import { afterEach, describe, expect, it, vi } from 'vitest'
import { applyTheme, isDark, loadTheme, onSystemThemeChange, saveTheme, systemPrefersDark } from './theme.ts'

/** A fake of window.matchMedia that reports the given answer, and records the listeners it receives. */
function stubMatchMedia(prefersDark: boolean) {
  const listeners = new Set<() => void>()
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('dark') && prefersDark,
    addEventListener: (_type: string, listener: () => void) => listeners.add(listener),
    removeEventListener: (_type: string, listener: () => void) => listeners.delete(listener),
  }))
  return listeners
}

afterEach(() => {
  document.documentElement.classList.remove('dark')
  document.documentElement.style.colorScheme = ''
})

describe('loadTheme and saveTheme', () => {
  it('follows the system when nothing was chosen', () => {
    expect(loadTheme()).toBe('system')
  })

  it('remembers light and dark', () => {
    saveTheme('dark')
    expect(loadTheme()).toBe('dark')

    saveTheme('light')
    expect(loadTheme()).toBe('light')
  })

  it('stores nothing for "system", so the system keeps deciding', () => {
    saveTheme('dark')

    saveTheme('system')

    expect(localStorage.getItem('gastos.theme')).toBeNull()
    expect(loadTheme()).toBe('system')
  })

  it.each(['purple', '', 'DARK', '{"a":1}'])('ignores a stored value that is not a theme: "%s"', (value) => {
    localStorage.setItem('gastos.theme', value)

    expect(loadTheme()).toBe('system')
  })

  it('does not break when the browser refuses to store anything', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(loadTheme()).toBe('system')
    expect(() => saveTheme('dark')).not.toThrow()
    vi.restoreAllMocks()
  })
})

describe('isDark', () => {
  it('forces light or dark whatever the system says', () => {
    expect(isDark('dark', false)).toBe(true)
    expect(isDark('light', true)).toBe(false)
  })

  it('follows the system for "system"', () => {
    expect(isDark('system', true)).toBe(true)
    expect(isDark('system', false)).toBe(false)
  })
})

describe('systemPrefersDark', () => {
  it('reads the system setting', () => {
    stubMatchMedia(true)
    expect(systemPrefersDark()).toBe(true)
  })

  it('is false where the browser cannot be asked', () => {
    vi.stubGlobal('matchMedia', undefined)
    expect(systemPrefersDark()).toBe(false)
  })
})

describe('applyTheme', () => {
  it('puts the dark class and color scheme on the page, and takes them off for light', () => {
    applyTheme('dark')
    expect(document.documentElement).toHaveClass('dark')
    expect(document.documentElement.style.colorScheme).toBe('dark')

    applyTheme('light')
    expect(document.documentElement).not.toHaveClass('dark')
    expect(document.documentElement.style.colorScheme).toBe('light')
  })

  it('uses the system setting for "system"', () => {
    stubMatchMedia(true)

    applyTheme('system')

    expect(document.documentElement).toHaveClass('dark')
  })
})

describe('onSystemThemeChange', () => {
  it('calls back on a change until the returned function is used', () => {
    const listeners = stubMatchMedia(false)
    const callback = vi.fn()

    const stop = onSystemThemeChange(callback)
    expect(listeners.size).toBe(1)
    stop()

    expect(listeners.size).toBe(0)
  })

  it('does nothing, without failing, where the browser cannot be asked', () => {
    vi.stubGlobal('matchMedia', undefined)

    expect(() => onSystemThemeChange(vi.fn())()).not.toThrow()
  })
})
