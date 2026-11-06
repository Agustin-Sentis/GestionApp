import { useEffect, useState } from 'react'
import { applyTheme, loadTheme, onSystemThemeChange, saveTheme, THEMES } from '../theme.ts'
import type { Theme } from '../theme.ts'

const LABELS: Record<Theme, string> = { system: 'Sistema', light: 'Claro', dark: 'Oscuro' }

/** Lets the user pick light, dark or "like my system". The choice is remembered in this browser. */
export default function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(loadTheme)

  useEffect(() => {
    applyTheme(theme)
    // Only "system" has to react when the operating system switches between light and dark
    if (theme !== 'system') return
    return onSystemThemeChange(() => applyTheme('system'))
  }, [theme])

  return (
    <label className="inline-flex items-center gap-2 text-sm">
      <span className="muted">Tema</span>
      <select
        className="input py-1.5"
        value={theme}
        onChange={(e) => {
          const next = e.target.value as Theme
          setTheme(next)
          saveTheme(next)
        }}
      >
        {THEMES.map((value) => (
          <option key={value} value={value}>{LABELS[value]}</option>
        ))}
      </select>
    </label>
  )
}
