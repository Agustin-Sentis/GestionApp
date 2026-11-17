import { useCallback, useEffect, useId, useRef, useState } from 'react'
import type { KeyboardEvent, ReactNode } from 'react'

interface Props {
  label: string
  /** The label is read by a screen but not drawn (the field then sits next to other controls that explain it). */
  hideLabel?: boolean
  /** What the closed field shows (already formatted). Empty shows the placeholder. */
  display: string
  placeholder: string
  /** Classes for the box around the field, to place it in its row. */
  className?: string
  /** Classes for the button that looks like the field. */
  triggerClassName?: string
  /** What opens under the field. It gets a function to close it (giving the focus back to the field). */
  children: (close: () => void) => ReactNode
}

// About the widest thing that opens under a field (a calendar of 7 days on a touch screen), to decide which side to open towards
const POPOVER_WIDTH = 304
const SCREEN_MARGIN = 8

function CalendarIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" className="shrink-0">
      <rect x="3" y="5" width="18" height="16" rx="1" />
      <path d="M3 10h18M8 3v4M16 3v4" />
    </svg>
  )
}

/**
 * A field that looks like the other inputs and opens something under it (a calendar, a list of months) instead of
 * showing the browser's own picker, which cannot take the look of the app. The button names itself with the label and
 * the value ("Fecha 17/11/2026"). Esc, a click outside or moving the focus away close it.
 */
export default function PickerField({ label, hideLabel = false, display, placeholder, className = '', triggerClassName = '', children }: Props) {
  const id = useId()
  const labelId = `${id}-label`
  const popoverId = `${id}-popover`
  const [open, setOpen] = useState(false)
  const [alignRight, setAlignRight] = useState(false)
  const root = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)

  // Looks the button up by id (not by ref): this function is handed to what opens under the field, which draws while rendering
  const close = useCallback(() => {
    setOpen(false)
    document.getElementById(id)?.focus()
  }, [id])

  useEffect(() => {
    if (!open) return
    const closeWhenOutside = (event: Event) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false)
    }
    // pointerdown for a click or a tap, focusin for Tab (focusin does not fire when a browser does not focus a button it was clicked on)
    document.addEventListener('pointerdown', closeWhenOutside)
    document.addEventListener('focusin', closeWhenOutside)
    return () => {
      document.removeEventListener('pointerdown', closeWhenOutside)
      document.removeEventListener('focusin', closeWhenOutside)
    }
  }, [open])

  function toggle() {
    if (!open && trigger.current) {
      // Beside the right edge of the screen the calendar opens towards the left, so it is not cut off
      const { left, right } = trigger.current.getBoundingClientRect()
      setAlignRight(left + POPOVER_WIDTH > window.innerWidth - SCREEN_MARGIN && right - POPOVER_WIDTH >= SCREEN_MARGIN)
    }
    setOpen(!open)
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape' && open) {
      event.stopPropagation() // inside a window (Ajustes) Esc closes only the calendar, not the window
      close()
    }
  }

  return (
    <div ref={root} className={`relative flex min-w-0 flex-col gap-1 ${className}`} onKeyDown={onKeyDown}>
      <span id={labelId} className={hideLabel ? 'sr-only' : 'text-xs font-semibold tracking-wide text-ink-soft uppercase'}>
        {label}
      </span>
      <button
        ref={trigger}
        id={id}
        type="button"
        className={`input flex w-full min-w-0 cursor-pointer items-center justify-between gap-2 text-left ${display ? '' : 'text-ink-soft/70'} ${triggerClassName}`}
        aria-labelledby={`${labelId} ${id}`}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls={open ? popoverId : undefined}
        onClick={toggle}
      >
        <span className="min-w-0 truncate">{display || placeholder}</span>
        <CalendarIcon />
      </button>

      {open && (
        <div
          id={popoverId}
          role="dialog"
          aria-label={label}
          className={`date-pop absolute top-full z-30 mt-1 max-w-[calc(100vw-1.5rem)] rounded-sm border-2 border-ink bg-sheet p-2 text-ink normal-case shadow-[4px_4px_0_var(--line)] ${
            alignRight ? 'right-0' : 'left-0'
          }`}
        >
          {children(close)}
        </div>
      )}
    </div>
  )
}
