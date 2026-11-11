import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api.ts'
import { useAuth } from '../auth.tsx'
import Brand, { APP_NAME } from '../components/Brand.tsx'
import PasswordRulesHint from '../components/PasswordRulesHint.tsx'
import ThemeToggle from '../components/ThemeToggle.tsx'

type Mode = 'signin' | 'signup' | 'forgot'

/** The landing page sends people who want a new account here with ?modo=registro, and the recovery mails with ?modo=recuperar. */
function initialMode(value: string | null): Mode {
  if (value === 'registro') return 'signup'
  if (value === 'recuperar') return 'forgot'
  return 'signin'
}

export default function AuthPage() {
  const { signIn, signUp } = useAuth()
  const [params] = useSearchParams()
  const [mode, setMode] = useState<Mode>(initialMode(params.get('modo')))
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  // The recovery mail was asked for. The answer is the same for every address, so it says nothing about the account.
  const [sent, setSent] = useState(false)

  function goTo(next: Mode) {
    setMode(next)
    setError(null)
    setSent(false)
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      if (mode === 'forgot') {
        await api.forgotPassword(email.trim())
        setSent(true)
        setBusy(false)
        return
      }
      await (mode === 'signin' ? signIn(email, password) : signUp(email, password))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ocurrió un error inesperado')
      setBusy(false)
    }
  }

  const isSignIn = mode === 'signin'
  const isForgot = mode === 'forgot'

  return (
    <div className="relative min-h-dvh">
      {/* The margin of the notebook page, as on the landing page */}
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
          <form className="card w-full max-w-sm gap-5 p-6" onSubmit={onSubmit}>
            <div className="flex flex-col gap-1">
              <h1 className="panel-title">{isForgot ? 'Recuperar contraseña' : isSignIn ? 'Entrar' : 'Nueva cuenta'}</h1>
              <p className="muted mt-2 text-sm">
                {isForgot
                  ? 'Te mandamos un enlace para elegir una contraseña nueva'
                  : isSignIn
                    ? 'Ingresá a tu cuenta'
                    : 'Creá tu cuenta'}
              </p>
            </div>

            {isForgot && sent ? (
              <>
                {/* The same words for every address: this must not tell whether the email has an account */}
                <p role="status">
                  Si ese correo tiene una cuenta, te mandamos un enlace para restablecer la contraseña. Vale 1 hora. Revisá
                  también la carpeta de spam.
                </p>
                <button type="button" className="btn-primary py-2.5" onClick={() => goTo('signin')}>
                  Volver a ingresar
                </button>
              </>
            ) : (
              <>
                <label className="field">
                  Email
                  <input
                    className="input"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    autoComplete="email"
                    required
                  />
                </label>

                {!isForgot && (
                  <label className="field">
                    Contraseña
                    <input
                      className="input"
                      type="password"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      autoComplete={isSignIn ? 'current-password' : 'new-password'}
                      required
                    />
                    {!isSignIn && <PasswordRulesHint />}
                  </label>
                )}

                {isSignIn && (
                  <button type="button" className="btn-link self-start" onClick={() => goTo('forgot')}>
                    ¿Olvidaste tu contraseña?
                  </button>
                )}

                {error && <p className="error-text" role="alert">{error}</p>}

                <button type="submit" className="btn-primary py-2.5" disabled={busy}>
                  {busy ? 'Un momento…' : isForgot ? 'Enviar enlace' : isSignIn ? 'Ingresar' : 'Crear cuenta'}
                </button>

                <button type="button" className="btn-link" onClick={() => goTo(isSignIn ? 'signup' : 'signin')}>
                  {isForgot ? 'Volver a ingresar' : isSignIn ? '¿No tenés cuenta? Registrate' : '¿Ya tenés cuenta? Ingresá'}
                </button>
              </>
            )}
          </form>
        </main>

        <footer className="pb-8">
          <Link to="/" className="btn-link p-0">← Volver al inicio</Link>
        </footer>
      </div>
    </div>
  )
}
