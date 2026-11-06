import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth.tsx'
import Brand, { APP_NAME } from '../components/Brand.tsx'
import ThemeToggle from '../components/ThemeToggle.tsx'

export default function AuthPage() {
  const { signIn, signUp } = useAuth()
  // The landing page sends people who want a new account here with ?modo=registro
  const [params] = useSearchParams()
  const [mode, setMode] = useState<'signin' | 'signup'>(params.get('modo') === 'registro' ? 'signup' : 'signin')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await (mode === 'signin' ? signIn(email, password) : signUp(email, password))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ocurrió un error inesperado')
      setBusy(false)
    }
  }

  const isSignIn = mode === 'signin'

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
              <h1 className="panel-title">{isSignIn ? 'Entrar' : 'Nueva cuenta'}</h1>
              <p className="muted mt-2 text-sm">{isSignIn ? 'Ingresá a tu cuenta' : 'Creá tu cuenta'}</p>
            </div>

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
              {!isSignIn && (
                <small className="mt-1 text-xs font-normal tracking-normal normal-case">
                  Mínimo 8 caracteres, con mayúscula, minúscula, número y un carácter especial.
                </small>
              )}
            </label>

            {error && <p className="error-text" role="alert">{error}</p>}

            <button type="submit" className="btn-primary py-2.5" disabled={busy}>
              {busy ? 'Un momento…' : isSignIn ? 'Ingresar' : 'Crear cuenta'}
            </button>

            <button
              type="button"
              className="btn-link"
              onClick={() => {
                setMode(isSignIn ? 'signup' : 'signin')
                setError(null)
              }}
            >
              {isSignIn ? '¿No tenés cuenta? Registrate' : '¿Ya tenés cuenta? Ingresá'}
            </button>
          </form>
        </main>

        <footer className="pb-8">
          <Link to="/" className="btn-link p-0">← Volver al inicio</Link>
        </footer>
      </div>
    </div>
  )
}
