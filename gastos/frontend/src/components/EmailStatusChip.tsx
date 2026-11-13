import { useQuery } from '@tanstack/react-query'
import { useEffect, useId, useRef, useState } from 'react'
import { api } from '../api.ts'
import EmailConfirmationActions from './EmailConfirmationActions.tsx'

/**
 * The notice in the header while the address of the account is not confirmed: a chip written in words ("Correo sin
 * confirmar"), never blocking anything. It opens a small panel with the two ways out: ask for the mail again, or correct the
 * address (see EmailConfirmationActions). The settings window has the same panel in its notifications section.
 * It disappears by itself once the address is confirmed: the settings are refreshed when the user comes back to the tab.
 */
export default function EmailStatusChip() {
  const panelId = useId()

  // The same query as the dollar control: one request, one cache
  const { data: settings } = useQuery({ queryKey: ['settings'], queryFn: api.settings.get })

  const [open, setOpen] = useState(false)

  const rootRef = useRef<HTMLDivElement>(null)
  const chipRef = useRef<HTMLButtonElement>(null)

  // Closing unmounts the panel, and with it whatever was typed in it: least of all a password stays around
  function close() {
    setOpen(false)
  }

  // Escape and a click outside close the panel. Only listening while it is open.
  useEffect(() => {
    if (!open) return
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      setOpen(false)
      chipRef.current?.focus()
    }
    function onPointerDown(event: PointerEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('keydown', onKeyDown)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [open])

  // Nothing to say while the saved state is unknown (still loading, or the request failed) or once the address is confirmed
  if (!settings || settings.emailConfirmed !== false) return null

  return (
    <div ref={rootRef} className="relative">
      <button
        ref={chipRef}
        type="button"
        className="inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-sm border-2 border-ink px-2.5 text-xs font-bold text-ink hover:bg-mark hover:text-on-mark focus-visible:outline-3 focus-visible:outline-pen"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        onClick={() => (open ? close() : setOpen(true))}
      >
        <span aria-hidden="true">✉</span>
        {/* On a phone only the envelope is drawn; the words stay for whoever listens to the page */}
        <span className="max-sm:sr-only">Correo sin confirmar</span>
      </button>

      {open && (
        <div
          id={panelId}
          role="dialog"
          aria-label="Correo sin confirmar"
          // On a phone it takes the width of the screen, so it can never be cut off by an edge
          className="z-30 flex flex-col gap-3 rounded-sm border-2 border-ink bg-sheet p-3 text-sm text-ink shadow-lg max-sm:fixed max-sm:inset-x-3 max-sm:top-20 sm:absolute sm:top-full sm:right-0 sm:mt-2 sm:w-80"
        >
          <EmailConfirmationActions email={settings.email} onResent={close} />
        </div>
      )}
    </div>
  )
}
