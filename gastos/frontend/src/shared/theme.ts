/** What the user chose: follow the system, or force light or dark. */
export type Theme = 'system' | 'light' | 'dark'

export const THEMES: readonly Theme[] = ['system', 'light', 'dark']

const THEME_KEY = 'gastos.theme'
const DARK_QUERY = '(prefers-color-scheme: dark)'

export function loadTheme(): Theme {
  try {
    const stored = localStorage.getItem(THEME_KEY)
    return stored === 'light' || stored === 'dark' ? stored : 'system'
  } catch {
    return 'system'
  }
}

export function saveTheme(theme: Theme): void {
  try {
    // "System" is the default: nothing is stored, so a later change of the system's own setting is followed
    if (theme === 'system') localStorage.removeItem(THEME_KEY)
    else localStorage.setItem(THEME_KEY, theme)
  } catch {
    // Without localStorage (private mode, etc.) the choice lasts until the page is reloaded
  }
}

/** Whether the system (browser or operating system) is set to dark. False where that cannot be asked. */
export function systemPrefersDark(): boolean {
  return window.matchMedia?.(DARK_QUERY).matches ?? false
}

export function isDark(theme: Theme, systemDark = systemPrefersDark()): boolean {
  return theme === 'dark' || (theme === 'system' && systemDark)
}

/** Puts the theme on the page: the "dark" class the styles look for, and the matching color scheme for form controls. */
export function applyTheme(theme: Theme): void {
  const dark = isDark(theme)
  document.documentElement.classList.toggle('dark', dark)
  document.documentElement.style.colorScheme = dark ? 'dark' : 'light'
}

/** Calls back when the system's own light/dark setting changes. Returns the function that stops listening. */
export function onSystemThemeChange(callback: () => void): () => void {
  const media = window.matchMedia?.(DARK_QUERY)
  if (!media) return () => {}
  media.addEventListener('change', callback)
  return () => media.removeEventListener('change', callback)
}
