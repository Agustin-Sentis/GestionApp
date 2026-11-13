import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from '../api.ts'
import { useToast } from './toastContext.ts'

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
export default function EmailConfirmationActions({ email: currentEmail, onResent }: Props) {
  const queryClient = useQueryClient()
  const toast = useToast()

  const [correcting, setCorrecting] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
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

  function submit(event: FormEvent) {
    event.preventDefault()
    const typed = email.trim()
    if (typed === '' || password === '') {
      setFormError('Escribí el correo nuevo y tu contraseña.')
      return
    }
    setFormError(null)
    change.mutate({ email: typed, password })
  }

  const busy = resend.isPending || change.isPending

  if (correcting) {
    return (
      <form aria-label="Cambiar el correo" noValidate className="flex flex-col gap-3" onSubmit={submit}>
        <label className="field">
          Correo nuevo
          <input
            ref={emailRef}
            className="input"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        <label className="field">
          Tu contraseña
          <input
            className="input"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>

        {formError && <p className="error-text" role="alert">{formError}</p>}

        <div className="flex items-center gap-2">
          <button type="submit" className="btn-primary" disabled={busy}>
            {change.isPending ? 'Guardando…' : 'Guardar'}
          </button>
          <button
            type="button"
            className="btn"
            disabled={busy}
            onClick={() => {
              setCorrecting(false)
              setPassword('')
              setFormError(null)
            }}
          >
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
        <button type="button" className="btn" disabled={busy} onClick={() => resend.mutate()}>
          Reenviar el correo
        </button>
        <button
          type="button"
          className="btn-link"
          onClick={() => {
            setEmail('')
            setCorrecting(true)
          }}
        >
          ¿Te equivocaste de correo?
        </button>
      </div>
    </>
  )
}
