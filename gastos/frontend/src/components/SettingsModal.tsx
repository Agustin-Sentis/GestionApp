import { useQuery } from '@tanstack/react-query'
import { useEffect, useId, useRef, useState } from 'react'
import type { PointerEvent as ReactPointerEvent } from 'react'
import { api } from '../api.ts'
import BudgetAlertsToggle from './BudgetAlertsToggle.tsx'
import EmailConfirmationActions from './EmailConfirmationActions.tsx'
import TwoFactorPanel from './TwoFactorPanel.tsx'

export type SettingsSection = 'security' | 'notifications'

const SECTIONS: { id: SettingsSection; label: string }[] = [
  { id: 'security', label: 'Seguridad' },
  { id: 'notifications', label: 'Notificaciones' },
]

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

interface Props {
  open: boolean
  section: SettingsSection
  onSectionChange: (section: SettingsSection) => void
  onClose: () => void
}

/**
 * The settings window: security (the two-step verification) and notifications (the address and the budget alert mails).
 * It is a modal: the focus stays inside while it is open and goes back to what opened it. Escape, the close button and a
 * click outside close it, except while the recovery codes are on screen: they are shown once, so nothing closes it (or
 * changes the section) until the user says they saved them.
 */
export default function SettingsModal({ open, section, onSectionChange, onClose }: Props) {
  const titleId = useId()
  const dialogRef = useRef<HTMLDivElement>(null)
  // The recovery codes are on screen (see TwoFactorPanel)
  const [blocked, setBlocked] = useState(false)

  // Focus goes into the window when it opens and back to where it was when it closes
  useEffect(() => {
    if (!open) return
    const previous = document.activeElement as HTMLElement | null
    dialogRef.current?.focus()
    return () => previous?.focus()
  }, [open])

  // Escape closes; Tab goes round inside the window instead of leaving it
  useEffect(() => {
    if (!open) return
    function onKeyDown(event: KeyboardEvent) {
      const dialog = dialogRef.current
      if (!dialog) return

      if (event.key === 'Escape') {
        if (!blocked) onClose()
        return
      }
      if (event.key !== 'Tab') return

      const items = Array.from(dialog.querySelectorAll<HTMLElement>(FOCUSABLE))
      if (items.length === 0) {
        event.preventDefault()
        dialog.focus()
        return
      }
      const first = items[0]
      const last = items[items.length - 1]
      const active = document.activeElement
      if (event.shiftKey && (active === first || active === dialog || !dialog.contains(active))) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && (active === last || !dialog.contains(active))) {
        event.preventDefault()
        first.focus()
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open, blocked, onClose])

  if (!open) return null

  // Only a press on the backdrop itself: one that starts inside the window (selecting text) and ends outside does not count
  function onBackdropPress(event: ReactPointerEvent<HTMLDivElement>) {
    if (event.target === event.currentTarget && !blocked) onClose()
  }

  const blockedHint = blocked ? 'Primero guardá tus códigos de recuperación' : undefined

  return (
    <div
      data-testid="settings-backdrop"
      className="fixed inset-0 z-40 grid place-items-center bg-ink/50 p-3"
      onPointerDown={onBackdropPress}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className="flex max-h-[90dvh] w-full max-w-lg flex-col rounded-sm border-2 border-ink bg-sheet text-ink shadow-lg outline-none"
      >
        <div className="flex items-center justify-between gap-3 border-b-2 border-ink px-4 py-3">
          <h2 id={titleId} className="panel-title">Ajustes</h2>
          <button
            type="button"
            className="btn px-2.5"
            aria-label="Cerrar ajustes"
            title={blockedHint}
            disabled={blocked}
            onClick={onClose}
          >
            ✕
          </button>
        </div>

        <div role="tablist" aria-label="Secciones de ajustes" className="grid grid-cols-2 border-b-2 border-ink">
          {SECTIONS.map((option) => (
            <button
              key={option.id}
              type="button"
              role="tab"
              id={`settings-tab-${option.id}`}
              aria-selected={section === option.id}
              aria-controls={`settings-panel-${option.id}`}
              disabled={blocked && section !== option.id}
              title={blocked && section !== option.id ? blockedHint : undefined}
              onClick={() => onSectionChange(option.id)}
              className={`display cursor-pointer px-2 py-2.5 text-sm uppercase disabled:cursor-not-allowed disabled:opacity-50 ${
                section === option.id ? 'bg-mark text-on-mark' : 'text-ink-soft hover:text-ink'
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>

        <div
          role="tabpanel"
          id={`settings-panel-${section}`}
          aria-labelledby={`settings-tab-${section}`}
          className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto p-4"
        >
          {section === 'security' ? <TwoFactorPanel onBlockingChange={setBlocked} /> : <NotificationsSection />}
        </div>
      </div>
    </div>
  )
}

/** The address of the account (and, while it is unconfirmed, the way to fix it) and the switch of the budget alert mails. */
function NotificationsSection() {
  // The same query as every other settings control: one request, one cache
  const { data: settings } = useQuery({ queryKey: ['settings'], queryFn: api.settings.get })

  if (!settings) return null

  return (
    <>
      <section className="flex flex-col gap-3" aria-labelledby="settings-email-title">
        <h3 id="settings-email-title" className="panel-title text-base">Correo</h3>
        <p className="flex flex-wrap items-center gap-2 text-sm">
          <strong>{settings.email}</strong>
          <span className="rounded-sm border-2 border-ink px-2 py-0.5 text-xs font-bold">
            {settings.emailConfirmed ? 'Confirmado' : 'Sin confirmar'}
          </span>
        </p>
        {!settings.emailConfirmed && (
          <div className="flex flex-col gap-3 text-sm">
            <EmailConfirmationActions email={settings.email} />
          </div>
        )}
      </section>

      <BudgetAlertsToggle />
    </>
  )
}
