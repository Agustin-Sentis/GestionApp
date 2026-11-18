import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'

interface Props {
  /** What the ✕ button is called for assistive technology, e.g. "Eliminar movimiento de Comida". */
  label: string
  onConfirm: () => void
  /** True while the deletion is being sent: nothing can be pressed again meanwhile. */
  pending?: boolean
}

/**
 * A delete button that asks before it does anything. Pressing ✕ swaps it for "¿Eliminar? Sí · No" in the same
 * place (no browser dialog, so it looks like the app and can be tested). "No" or Escape put the ✕ back.
 */
const ConfirmDelete = ({ label, onConfirm, pending = false }: Props) => {
  const [asking, setAsking] = useState(false)
  const openRef = useRef<HTMLButtonElement>(null)
  const cancelRef = useRef<HTMLButtonElement>(null)
  const wasAsking = useRef(false)

  // Keyboard and screen reader users must not lose their place: focus follows the swap in both directions.
  // The safe answer ("No") gets the focus, so pressing Enter by reflex never deletes anything.
  useEffect(() => {
    if (asking) cancelRef.current?.focus()
    else if (wasAsking.current) openRef.current?.focus()
    wasAsking.current = asking
  }, [asking])

  const handleAsk = () => setAsking(true)

  const handleCancel = () => setAsking(false)

  const handleConfirm = () => {
    setAsking(false)
    onConfirm()
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLSpanElement>) => {
    if (e.key === 'Escape') setAsking(false)
  }

  if (!asking) {
    return (
      <button ref={openRef} type="button" className="btn-icon" onClick={handleAsk} disabled={pending} aria-label={label} title="Eliminar">
        ✕
      </button>
    )
  }

  return (
    <span
      role="group"
      aria-label="Confirmar eliminación"
      className="inline-flex items-center gap-2 text-sm whitespace-nowrap"
      onKeyDown={handleKeyDown}
    >
      <span className="muted">¿Eliminar?</span>
      <button
        type="button"
        className="cursor-pointer font-bold text-expense underline decoration-2 underline-offset-4 hover:bg-expense hover:text-sheet disabled:cursor-not-allowed disabled:opacity-50"
        disabled={pending}
        onClick={handleConfirm}
      >
        Sí
      </button>
      <button ref={cancelRef} type="button" className="btn-link" onClick={handleCancel}>
        No
      </button>
    </span>
  )
}

export default ConfirmDelete
