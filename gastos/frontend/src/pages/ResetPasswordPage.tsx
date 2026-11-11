import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, LINK_EXPIRED_MESSAGE } from '../api.ts'
import { useAuth } from '../auth.tsx'
import Brand, { APP_NAME } from '../components/Brand.tsx'
import PasswordRulesHint from '../components/PasswordRulesHint.tsx'
import ThemeToggle from '../components/ThemeToggle.tsx'

/**
 * Where the link of the recovery mail lands (/restablecer-contrasena?token=…). It is public: the link may be opened on another
 * device. The new password is asked twice, and the two are compared here, before anything is sent. When it works, the session
 * this browser may have is closed on the spot: the server stops accepting those anyway, and this saves a bounce.
 */
export default function ResetPasswordPage() {
  const [params] = useSearchParams()
  const token = params.get('token')?.trim() ?? ''
  const { logout } = useAuth()

  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [linkIsDead, setLinkIsDead] = useState(false)
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    if (password !== repeat) {
      setError('Las contraseñas no coinciden.')
      return
    }

    setBusy(true)
    try {
      await api.resetPassword(token, password)
      logout()
      setDone(true)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Ocurrió un error inesperado'
      // An expired link cannot be fixed by trying again; a password that breaks the rules can
      setLinkIsDead(message === LINK_EXPIRED_MESSAGE)
      setError(message)
      setBusy(false)
    }
  }

  const needsNewLink = token === '' || linkIsDead

  return (
    <div className="relative min-h-dvh">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-y-0 left-3 w-[5px] border-r-4 border-double border-margin sm:left-14"
      />

      <div className="mx-auto flex min-h-dvh max-w-6xl flex-col pr-4 pl-8 sm:pr-8 sm:pl-24">
        <header className="flex items-center justify-between gap-4 py-5">
          <Link to="/" aria-label={`${APP_NAME}, volver al inicio`}><Brand /></Link>
          <ThemeToggle />
        </header>

        <main className="grid flex-1 place-items-center py-8">
          <section className="card w-full max-w-sm gap-5 p-6">
            <h1 className="panel-title">Nueva contraseña</h1>

            {done ? (
              <>
                <p role="status">
                  <strong>Contraseña cambiada.</strong> Ya podés ingresar con la nueva. Las sesiones que estaban abiertas se cerraron.
                </p>
                <Link to="/login" className="btn-primary py-2.5 text-center">Ir a ingresar</Link>
              </>
            ) : needsNewLink ? (
              <>
                <p className="error-text" role="alert">
                  {token === ''
                    ? 'Este enlace no trae el código para restablecer la contraseña. Abrí el enlace completo del correo.'
                    : (error ?? LINK_EXPIRED_MESSAGE)}
                </p>
                <Link to="/login?modo=recuperar" className="btn-primary py-2.5 text-center">Pedir uno nuevo</Link>
              </>
            ) : (
              <form className="flex flex-col gap-5" onSubmit={onSubmit}>
                <label className="field">
                  Contraseña nueva
                  <input
                    className="input"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    autoComplete="new-password"
                    required
                  />
                  <PasswordRulesHint />
                </label>

                <label className="field">
                  Repetir la contraseña
                  <input
                    className="input"
                    type="password"
                    value={repeat}
                    onChange={(e) => setRepeat(e.target.value)}
                    autoComplete="new-password"
                    required
                  />
                </label>

                {error && <p className="error-text" role="alert">{error}</p>}

                <button type="submit" className="btn-primary py-2.5" disabled={busy}>
                  {busy ? 'Un momento…' : 'Cambiar contraseña'}
                </button>
              </form>
            )}
          </section>
        </main>
      </div>
    </div>
  )
}
