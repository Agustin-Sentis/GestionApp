import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { ToastContext } from './toastContext.ts'
import type { ToastAction, ToastApi, ToastOptions, ToastTone } from './toastContext.ts'

interface Toast {
  id: number
  tone: ToastTone
  message: string
  action?: ToastAction
}

/** How long each kind stays on screen. Errors stay longer: they are the ones people need time to read. */
const DURATION_MS: Record<ToastTone, number> = { success: 5000, info: 5000, error: 8000 }

/** A notice with a button needs time to be read AND answered. */
const ACTION_DURATION_MS = 8000

/** More than this at once is noise; the oldest one makes room. */
const MAX_VISIBLE = 4

const TONE_STYLES: Record<ToastTone, string> = {
  success: 'border-l-pen',
  error: 'border-l-expense',
  info: 'border-l-ink-soft',
}

export default function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(0)
  const timers = useRef(new Map<number, ReturnType<typeof setTimeout>>())

  const dismiss = useCallback((id: number) => {
    clearTimeout(timers.current.get(id))
    timers.current.delete(id)
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  const show = useCallback(
    (tone: ToastTone, message: string, options?: ToastOptions) => {
      const id = nextId.current++
      const action = options?.action
      setToasts((current) => [...current, { id, tone, message, action }].slice(-MAX_VISIBLE))
      const duration = action ? Math.max(DURATION_MS[tone], ACTION_DURATION_MS) : DURATION_MS[tone]
      timers.current.set(id, setTimeout(() => dismiss(id), duration))
    },
    [dismiss],
  )

  // No timer may fire after the provider is gone
  useEffect(() => {
    const pending = timers.current
    return () => {
      pending.forEach(clearTimeout)
      pending.clear()
    }
  }, [])

  // Stable object, so components that call useToast do not re-render (or re-run effects) on every new notice
  const api = useMemo<ToastApi>(
    () => ({
      success: (message, options) => show('success', message, options),
      error: (message, options) => show('error', message, options),
      info: (message, options) => show('info', message, options),
    }),
    [show],
  )

  return (
    <ToastContext.Provider value={api}>
      {children}

      <div
        aria-live="polite"
        className="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-stretch gap-2 sm:inset-x-auto sm:right-4 sm:w-96"
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            // An error interrupts a screen reader; the rest wait for a pause
            role={toast.tone === 'error' ? 'alert' : 'status'}
            className={`pointer-events-auto motion-safe:animate-toast-in flex items-start gap-3 rounded-sm border-2 border-l-8 border-ink bg-sheet py-3 pr-2 pl-4 text-sm font-medium text-ink ${TONE_STYLES[toast.tone]}`}
          >
            <p className="min-w-0 flex-1 wrap-anywhere">{toast.message}</p>
            {toast.action && (
              <button
                type="button"
                className="btn-link font-semibold"
                onClick={() => {
                  toast.action?.onClick()
                  dismiss(toast.id) // the notice did its job
                }}
              >
                {toast.action.label}
              </button>
            )}
            <button
              type="button"
              className="btn-icon"
              onClick={() => dismiss(toast.id)}
              aria-label="Cerrar aviso"
            >
              ✕
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
