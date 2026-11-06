import { useEffect, useId, useState } from 'react'
import type { ReactNode } from 'react'
import { applyTheme, loadTheme, onSystemThemeChange, saveTheme, THEMES } from '../theme.ts'
import type { Theme } from '../theme.ts'

/** Small drawings in the style of the rest of the page: a notebook page, the chalkboard, and a square split between both. */
const ICONS: Record<Theme, ReactNode> = {
  // A page of the notebook: the grid
  light: (
    <>
      <rect x="2" y="2" width="12" height="12" rx="1.5" />
      <path d="M2 6h12M2 10h12M6 2v12M10 2v12" strokeWidth="1" />
    </>
  ),
  // The chalkboard: the frame, a line of chalk and the tray
  dark: (
    <>
      <rect x="1.5" y="2.5" width="13" height="9" rx="1.5" />
      <path d="M4 8c1-2.5 2 1.5 3.2-.5s2 1 3.3-.8" />
      <path d="M3 14h10" />
    </>
  ),
  // Half page, half board
  system: (
    <>
      <rect x="2" y="2" width="12" height="12" rx="1.5" />
      <path d="M14 2L2 14" />
      <path d="M14 2v12H2z" fill="currentColor" stroke="none" />
    </>
  ),
}

/** What each option is called for someone who cannot see the drawing. */
const LABELS: Record<Theme, string> = {
  system: 'Sistema',
  light: 'Hoja (claro)',
  dark: 'Pizarrón (oscuro)',
}

/** What the drawing means, shown on hover and focus: the short name and one line that says what it does. */
const HINTS: Record<Theme, { title: string; text: string }> = {
  system: { title: 'Sistema', text: 'Usa el modo claro u oscuro de tu dispositivo.' },
  light: { title: 'Hoja', text: 'Modo claro, como una hoja cuadriculada.' },
  dark: { title: 'Pizarrón', text: 'Modo oscuro, como un pizarrón con tiza.' },
}

/**
 * Lets the user pick the page ("hoja", light), the chalkboard ("pizarrón", dark) or "like my system".
 * The three choices are always in view, as radio buttons: the arrow keys move between them, and the choice is remembered.
 * Hovering or focusing one explains it in a small note under the control.
 */
export default function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(loadTheme)
  const idPrefix = useId()

  useEffect(() => {
    applyTheme(theme)
    // Only "system" has to react when the operating system switches between light and dark
    if (theme !== 'system') return
    return onSystemThemeChange(() => applyTheme('system'))
  }, [theme])

  return (
    // No overflow clipping here: the note hangs below the control and must not be cut off
    <fieldset className="relative inline-flex divide-x-2 divide-ink rounded-sm border-2 border-ink">
      <legend className="sr-only">Tema</legend>
      {THEMES.map((value) => {
        const hintId = `${idPrefix}-${value}`
        return (
          <div key={value} className="group">
            <label className="grid size-9 cursor-pointer place-items-center text-ink-soft hover:text-ink has-checked:bg-mark has-checked:text-on-mark has-focus-visible:outline-3 has-focus-visible:-outline-offset-3 has-focus-visible:outline-pen">
              <input
                type="radio"
                name="theme"
                value={value}
                checked={theme === value}
                aria-describedby={hintId}
                onChange={() => {
                  setTheme(value)
                  saveTheme(value)
                }}
                className="sr-only"
              />
              <svg viewBox="0 0 16 16" aria-hidden="true" className="size-4 fill-none stroke-current" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                {ICONS[value]}
              </svg>
              <span className="sr-only">{LABELS[value]}</span>
            </label>

            {/* Outside the label, so it is the description of the choice and not part of its name */}
            <span
              id={hintId}
              role="tooltip"
              className="pointer-events-none invisible absolute top-full left-1/2 z-30 mt-2 w-max max-w-44 -translate-x-1/2 rounded-sm border-2 border-ink bg-sheet px-2.5 py-1.5 text-left text-xs leading-snug font-medium text-ink opacity-0 transition-opacity delay-100 group-hover:visible group-hover:opacity-100 group-has-focus-visible:visible group-has-focus-visible:opacity-100 motion-reduce:transition-none"
            >
              <strong className="display block text-[0.7rem] uppercase" style={{ letterSpacing: '0.05em' }}>
                {HINTS[value].title}
              </strong>
              {HINTS[value].text}
            </span>
          </div>
        )
      })}
    </fieldset>
  )
}
