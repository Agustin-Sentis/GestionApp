import { useQuery } from '@tanstack/react-query'
import { useEffect, useId, useRef, useState } from 'react'
import type { PointerEvent as ReactPointerEvent } from 'react'
import { api } from '../../Services/api/api.ts'
import BudgetAlertsToggle from '../BudgetAlertsToggle/BudgetAlertsToggle.tsx'
import EmailConfirmationActions from '../EmailConfirmationActions/EmailConfirmationActions.tsx'
import ThemeToggle from '../ThemeToggle/ThemeToggle.tsx'
import TwoFactorPanel from '../TwoFactorPanel/TwoFactorPanel.tsx'

export type SettingsSection = 'security' | 'notifications' | 'appearance'

const SECTIONS: { id: SettingsSection; label: string }[] = [
  { id: 'security', label: 'Seguridad' },
  { id: 'notifications', label: 'Notificaciones' },
  { id: 'appearance', label: 'Apariencia' },
]

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

interface Props {
  open: boolean
  section: SettingsSection
  onSectionChange: (section: SettingsSection) => void
  onClose: () => void
}

/**
 * The settings window: security (the two-step verification), notifications (the address and the budget alert mails) and
 * appearance (the theme: it is not in the dashboard header any more, to leave room for the figures).
 * It is a modal: the focus stays inside while it is open and goes back to what opened it. Escape, the close button and a
 * click outside close it, except while the recovery codes are on screen: they are shown once, so nothing closes it (or
 * changes the section) until the user says they saved them.
 */
const SettingsModal = ({ open, section, onSectionChange, onClose }: Props) => {
  // The recovery codes are on screen (see TwoFactorPanel)
  const [blocked, setBlocked] = useState(false)

  const titleId = useId()
  const dialogRef = useRef<HTMLDivElement>(null)

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
    const handleKeyDown = (event: KeyboardEvent) => {
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
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, blocked, onClose])

  // Only a press on the backdrop itself: one that starts inside the window (selecting text) and ends outside does not count
  const handleBackdropPress = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.target === event.currentTarget && !blocked) onClose()
  }

  const handleChooseSection = (id: SettingsSection) => () => onSectionChange(id)

  const handleBlockingChange = (isBlocked: boolean) => setBlocked(isBlocked)

  if (!open) return null

  const blockedHint = blocked ? 'Primero guardá tus códigos de recuperación' : undefined

  return (
    <div
      data-testid="settings-backdrop"
      className="fixed inset-0 z-40 grid place-items-center bg-ink/50 p-3"
      onPointerDown={handleBackdropPress}
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
            className="btn px-2.5 pointer-coarse:min-w-11"
            aria-label="Cerrar ajustes"
            title={blockedHint}
            disabled={blocked}
            onClick={onClose}
          >
            ✕
          </button>
        </div>

        <div role="tablist" aria-label="Secciones de ajustes" className="flex border-b-2 border-ink">
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
              onClick={handleChooseSection(option.id)}
              // A third of a narrow window: normal-width letters and a smaller size there, the wide ones from sm up
              // (the same as the tabs at the foot of the dashboard). truncate is the last resort against overlapping labels.
              className={`display min-w-0 flex-auto cursor-pointer truncate px-1 py-2.5 text-[0.7rem] uppercase [font-stretch:100%] disabled:cursor-not-allowed disabled:opacity-50 pointer-coarse:min-h-11 sm:px-2 sm:text-sm sm:[font-stretch:125%] ${
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
          {section === 'security' && <TwoFactorPanel onBlockingChange={handleBlockingChange} />}
          {section === 'notifications' && <NotificationsSection />}
          {section === 'appearance' && <AppearanceSection />}
        </div>
      </div>
    </div>
  )
}

/** How the app looks: the page (light), the chalkboard (dark) or whatever the device uses. */
const AppearanceSection = () => {
  return (
    <section className="flex flex-col gap-3" aria-labelledby="settings-theme-title">
      <h3 id="settings-theme-title" className="panel-title text-base">Tema</h3>
      <ThemeToggle showLabels />
    </section>
  )
}

/** The address of the account (and, while it is unconfirmed, the way to fix it) and the switch of the budget alert mails. */
const NotificationsSection = () => {
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

export default SettingsModal
