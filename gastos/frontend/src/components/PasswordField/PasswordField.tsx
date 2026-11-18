import { useId, useState } from 'react'
import type { ChangeEvent, ReactNode } from 'react'
import FieldError from '../FieldError/FieldError.tsx'

type Props = {
  label: string
  value: string
  onChange: (value: string) => void
  autoComplete: 'current-password' | 'new-password'
  placeholder?: string
  required?: boolean
  autoFocus?: boolean
  /** What is wrong with the value, if anything: it is written under the field. */
  error?: string
  /** Text under the input (for example the rules of a new password). */
  children?: ReactNode
}

const EyeIcon = ({ crossed }: { crossed: boolean }) => {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z" />
      <circle cx="12" cy="12" r="3" />
      {crossed && <path d="M4 4l16 16" />}
    </svg>
  )
}

/**
 * A password field with a button to see what was typed. It starts hidden, and the button is not inside the
 * <label>: that way the label (and so the accessible name of the input) is only its text.
 */
const PasswordField = ({ label, value, onChange, autoComplete, placeholder, required, autoFocus, error, children }: Props) => {
  const [visible, setVisible] = useState(false)
  const id = useId()

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => onChange(e.target.value)

  const handleToggleVisible = () => setVisible((v) => !v)

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <div className="relative flex">
        <input
          id={id}
          className="input w-full pr-11"
          type={visible ? 'text' : 'password'}
          value={value}
          onChange={handleChange}
          autoComplete={autoComplete}
          placeholder={placeholder}
          required={required}
          autoFocus={autoFocus}
          aria-invalid={error ? true : undefined}
        />
        <button
          type="button"
          className="btn-icon absolute inset-y-0 right-0 flex items-center justify-center px-2.5 hover:text-ink pointer-coarse:min-w-11"
          aria-label="Mostrar contraseña"
          aria-pressed={visible}
          onClick={handleToggleVisible}
        >
          <EyeIcon crossed={visible} />
        </button>
      </div>
      <FieldError>{error}</FieldError>
      {children}
    </div>
  )
}

export default PasswordField
