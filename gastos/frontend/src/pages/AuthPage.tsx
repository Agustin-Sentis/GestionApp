import { useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../auth.tsx'

export default function AuthPage() {
  const { signIn, signUp } = useAuth()
  const [mode, setMode] = useState<'signin' | 'signup'>('signin')
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
    <main className="grid min-h-screen place-items-center p-4">
      <form className="card w-full max-w-sm gap-4 p-6" onSubmit={onSubmit}>
        <div>
          <h1 className="text-xl font-semibold">Gestor de gastos</h1>
          <p className="muted text-sm">{isSignIn ? 'Ingresá a tu cuenta' : 'Creá tu cuenta'}</p>
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
            <small>Mínimo 8 caracteres, con mayúscula, minúscula, número y un carácter especial.</small>
          )}
        </label>

        {error && <p className="error-text" role="alert">{error}</p>}

        <button type="submit" className="btn-primary" disabled={busy}>
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
  )
}
