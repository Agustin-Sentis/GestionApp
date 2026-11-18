import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { api } from '../../Services/api/api.ts'
import { FORM_ERROR_MESSAGE, hasErrors, validateEmail, validatePasswordPresent } from '../../shared/Validations/validations.ts'
import type { FormErrors } from '../../shared/Validations/validations.ts'
import FieldError from '../FieldError/FieldError.tsx'
import PasswordField from '../PasswordField/PasswordField.tsx'
import { useToast } from '../../Services/toastContext/ToastContext.ts'

interface Props {
  /** The address of the account, which has not been confirmed yet. */
  email: string
  /** A new confirmation mail was sent (the caller may want to close what holds this). */
  onResent?: () => void
}

/**
 * The two ways out of an unconfirmed address: ask for the mail again, or correct the address (a typo at the sign up is
 * exactly what leaves an account unconfirmed) after typing the password. It lives in the popover of the header chip and in
 * the notifications section of the settings. Nothing typed outlives it: when it is unmounted, the password goes with it.
 */
const EmailConfirmationActions = ({ email: currentEmail, onResent }: Props) => {
  const [correcting, setCorrecting] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  // What is wrong with each field, found before anything is sent
  const [errors, setErrors] = useState<FormErrors<'email' | 'password'>>({})

  const queryClient = useQueryClient()
  const toast = useToast()
  const emailRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (correcting) emailRef.current?.focus()
  }, [correcting])

  const resend = useMutation({
    mutationFn: api.settings.resendConfirmation,
    onSuccess: () => {
      toast.success(`Te mandamos otro correo a ${currentEmail}`)
      onResent?.()
    },
    // The server says why: one was sent a moment ago, or the mail could not be sent
    onError: (error) => toast.error(error.message),
  })

  const change = useMutation({
    mutationFn: ({ email, password }: { email: string; password: string }) => api.settings.changeEmail(email, password),
    onSuccess: (saved) => {
      queryClient.setQueryData(['settings'], saved)
      toast.success(`Te mandamos un correo a ${saved.email}`)
      setCorrecting(false)
      setPassword('')
      setFormError(null)
    },
    // Inside the form, next to the fields: a wrong password, an address that is taken, a mail that could not be sent
    onError: (error) => setFormError(error.message),
  })

  const handleChangeEmail = (e: ChangeEvent<HTMLInputElement>) => {
    setEmail(e.target.value)
    setErrors((previous) => ({ ...previous, email: '' }))
  }

  const handleChangePassword = (value: string) => {
    setPassword(value)
    setErrors((previous) => ({ ...previous, password: '' }))
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    const formErrors = { email: validateEmail(email), password: validatePasswordPresent(password) }
    setErrors(formErrors)
    if (hasErrors(formErrors)) {
      toast.error(FORM_ERROR_MESSAGE)
      return
    }
    setFormError(null)
    change.mutate({ email: email.trim(), password })
  }

  const handleCancel = () => {
    setCorrecting(false)
    setPassword('')
    setFormError(null)
    setErrors({})
  }

  const handleResend = () => resend.mutate()

  const handleStartCorrecting = () => {
    setEmail('')
    setCorrecting(true)
  }

  const busy = resend.isPending || change.isPending

  if (correcting) {
    return (
      <form aria-label="Cambiar el correo" noValidate className="flex flex-col gap-3" onSubmit={handleSubmit}>
        <label className="field">
          Correo nuevo
          <input
            ref={emailRef}
            className="input"
            type="email"
            autoComplete="email"
            value={email}
            onChange={handleChangeEmail}
            aria-invalid={errors.email ? true : undefined}
          />
        </label>
        <FieldError>{errors.email}</FieldError>
        <PasswordField
          label="Tu contraseña"
          value={password}
          onChange={handleChangePassword}
          autoComplete="current-password"
          error={errors.password}
        />

        {formError && <p className="error-text" role="alert">{formError}</p>}

        <div className="flex items-center gap-2">
          <button type="submit" className="btn-primary" disabled={busy}>
            {change.isPending ? 'Guardando…' : 'Guardar'}
          </button>
          <button type="button" className="btn" disabled={busy} onClick={handleCancel}>
            Cancelar
          </button>
        </div>
      </form>
    )
  }

  return (
    <>
      <p>
        Te mandamos un correo a <strong>{currentEmail}</strong>. Hacé clic en el enlace para confirmarlo. Mientras tanto
        podés usar la app como siempre.
      </p>
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <button type="button" className="btn" disabled={busy} onClick={handleResend}>
          Reenviar el correo
        </button>
        <button type="button" className="btn-link" onClick={handleStartCorrecting}>
          ¿Te equivocaste de correo?
        </button>
      </div>
    </>
  )
}

export default EmailConfirmationActions
