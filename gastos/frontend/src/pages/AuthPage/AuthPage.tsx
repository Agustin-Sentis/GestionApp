import { useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError, api } from '../../Services/api/api.ts'
import { useAuth } from '../../Services/authContext/AuthContext.ts'
import Brand, { APP_NAME } from '../../components/Brand/Brand.tsx'
import FieldError from '../../components/FieldError/FieldError.tsx'
import PasswordField from '../../components/PasswordField/PasswordField.tsx'
import PasswordRulesHint from '../../components/PasswordRulesHint/PasswordRulesHint.tsx'
import ThemeToggle from '../../components/ThemeToggle/ThemeToggle.tsx'
import { useToast } from '../../Services/toastContext/ToastContext.ts'
import {
  FORM_ERROR_MESSAGE,
  hasErrors,
  validateCode,
  validateEmail,
  validateEmailPresent,
  validateNewPassword,
  validatePasswordPresent,
} from '../../shared/Validations/validations.ts'
import type { FormErrors } from '../../shared/Validations/validations.ts'

type Mode = 'signin' | 'signup' | 'forgot'

/** The fields of this page that can have an error under them. */
type Field = 'email' | 'password' | 'code'

/** The landing page sends people who want a new account here with ?modo=registro, and the recovery mails with ?modo=recuperar. */
const initialMode = (value: string | null): Mode => {
  if (value === 'registro') return 'signup'
  if (value === 'recuperar') return 'forgot'
  return 'signin'
}

const AuthPage = () => {
  // The address says which form opens first (see initialMode), so it is read before the states
  const [params] = useSearchParams()

  const [mode, setMode] = useState<Mode>(initialMode(params.get('modo')))
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  // The recovery mail was asked for. The answer is the same for every address, so it says nothing about the account.
  const [sent, setSent] = useState(false)
  // The password was right and the account has the two-step verification: the second step is on screen until the code
  // is accepted. The challenge is all there is (no session yet), and it lasts 5 minutes.
  const [challenge, setChallenge] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [useRecoveryCode, setUseRecoveryCode] = useState(false)
  // What is wrong with each field, found before anything is sent
  const [errors, setErrors] = useState<FormErrors<Field>>({})

  const { signIn, signUp, verifyTwoFactor } = useAuth()
  const toast = useToast()

  const clearError = (field: Field) => setErrors((previous) => ({ ...previous, [field]: '' }))

  const leaveSecondStep = () => {
    setChallenge(null)
    setCode('')
    setUseRecoveryCode(false)
    setErrors({})
  }

  // Sign in only asks that the credentials are there; the rules of a new account (and a well-formed address) apply to the rest
  const validateForm = (): FormErrors<Field> => {
    if (challenge) return { code: validateCode(code) }
    if (mode === 'forgot') return { email: validateEmail(email) }
    if (mode === 'signin') return { email: validateEmailPresent(email), password: validatePasswordPresent(password) }
    return { email: validateEmail(email), password: validateNewPassword(password) }
  }

  const handleGoTo = (next: Mode) => () => {
    setMode(next)
    setError(null)
    setSent(false)
    leaveSecondStep()
  }

  const handleChangeEmail = (e: ChangeEvent<HTMLInputElement>) => {
    setEmail(e.target.value)
    clearError('email')
  }

  const handleChangePassword = (value: string) => {
    setPassword(value)
    clearError('password')
  }

  const handleChangeCode = (e: ChangeEvent<HTMLInputElement>) => {
    setCode(e.target.value)
    clearError('code')
  }

  const handleToggleRecoveryCode = () => {
    setUseRecoveryCode(!useRecoveryCode)
    setCode('')
    setError(null)
    setErrors({})
  }

  const handleBackToPassword = () => {
    setError(null)
    leaveSecondStep()
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)

    const formErrors = validateForm()
    setErrors(formErrors)
    if (hasErrors(formErrors)) {
      toast.error(FORM_ERROR_MESSAGE)
      return
    }

    setBusy(true)
    try {
      if (challenge) {
        await verifyTwoFactor(challenge, code.trim())
        return
      }
      if (mode === 'forgot') {
        await api.forgotPassword(email.trim())
        setSent(true)
        setBusy(false)
        return
      }
      if (mode === 'signin') {
        const next = await signIn(email, password)
        if (next) {
          // Do not keep the password around while the second step is open
          setPassword('')
          setChallenge(next.challengeToken)
          setBusy(false)
        }
        return
      }
      await signUp(email, password)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ocurrió un error inesperado')
      setBusy(false)
      if (challenge) {
        // The challenge expired: nothing to try again with, back to the password. Any other error leaves the step open.
        if (err instanceof ApiError && err.status === 401) leaveSecondStep()
        else setCode('')
      }
    }
  }

  const isSignIn = mode === 'signin'
  const isForgot = mode === 'forgot'
  const isSecondStep = challenge !== null

  return (
    <div className="relative min-h-dvh">
      {/* The margin of the notebook page, as on the landing page */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-y-0 left-3 w-[5px] border-r-4 border-double border-margin sm:left-14"
      />

      <div className="mx-auto flex min-h-dvh max-w-6xl flex-col pr-4 pl-8 sm:pr-8 sm:pl-24">
        <header className="flex flex-wrap items-center justify-between gap-4 py-5">
          <Link to="/" aria-label={`${APP_NAME}, volver al inicio`}><Brand /></Link>
          <ThemeToggle />
        </header>

        <main className="grid flex-1 place-items-center py-8">
          <form className="card w-full max-w-sm gap-5 p-6" onSubmit={handleSubmit} noValidate>
            <div className="flex flex-col gap-1">
              <h1 className="panel-title">
                {isSecondStep ? 'Verificación en dos pasos' : isForgot ? 'Recuperar contraseña' : isSignIn ? 'Entrar' : 'Nueva cuenta'}
              </h1>
              <p className="muted mt-2 text-sm">
                {isSecondStep
                  ? useRecoveryCode
                    ? 'Ingresá uno de tus códigos de recuperación. Cada uno sirve una sola vez.'
                    : 'Ingresá el código de 6 dígitos que muestra tu app autenticadora'
                  : isForgot
                    ? 'Te mandamos un enlace para elegir una contraseña nueva'
                    : isSignIn
                      ? 'Ingresá a tu cuenta'
                      : 'Creá tu cuenta'}
              </p>
            </div>

            {isSecondStep ? (
              <>
                <label className="field">
                  {useRecoveryCode ? 'Código de recuperación' : 'Código de 6 dígitos'}
                  {/* One field that changes kind, so the focus stays where the user is typing. A new key remounts it, empty. */}
                  <input
                    key={useRecoveryCode ? 'recovery' : 'app'}
                    className="input"
                    type="text"
                    value={code}
                    onChange={handleChangeCode}
                    inputMode={useRecoveryCode ? 'text' : 'numeric'}
                    autoComplete={useRecoveryCode ? 'off' : 'one-time-code'}
                    autoCapitalize={useRecoveryCode ? 'characters' : 'off'}
                    spellCheck={false}
                    aria-invalid={errors.code ? true : undefined}
                    autoFocus
                    required
                  />
                </label>
                <FieldError>{errors.code}</FieldError>

                {error && <p className="error-text" role="alert">{error}</p>}

                <button type="submit" className="btn-primary py-2.5" disabled={busy}>
                  {busy ? 'Un momento…' : 'Verificar'}
                </button>

                <button type="button" className="btn-link self-start" onClick={handleToggleRecoveryCode}>
                  {useRecoveryCode ? 'Usar el código de la app' : 'Usar un código de recuperación'}
                </button>

                <button type="button" className="btn-link" onClick={handleBackToPassword}>
                  Volver
                </button>
              </>
            ) : isForgot && sent ? (
              <>
                {/* The same words for every address: this must not tell whether the email has an account */}
                <p role="status">
                  Si ese correo tiene una cuenta, te mandamos un enlace para restablecer la contraseña. Vale 1 hora. Revisá
                  también la carpeta de spam.
                </p>
                <button type="button" className="btn-primary py-2.5" onClick={handleGoTo('signin')}>
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
                    onChange={handleChangeEmail}
                    autoComplete="email"
                    placeholder="tu@correo.com"
                    aria-invalid={errors.email ? true : undefined}
                    required
                  />
                </label>
                <FieldError>{errors.email}</FieldError>

                {!isForgot && (
                  <PasswordField
                    label="Contraseña"
                    value={password}
                    onChange={handleChangePassword}
                    autoComplete={isSignIn ? 'current-password' : 'new-password'}
                    placeholder={isSignIn ? 'Tu contraseña' : 'Creá una contraseña'}
                    error={errors.password}
                    required
                  >
                    {!isSignIn && <PasswordRulesHint />}
                  </PasswordField>
                )}

                {isSignIn && (
                  <button type="button" className="btn-link self-start" onClick={handleGoTo('forgot')}>
                    ¿Olvidaste tu contraseña?
                  </button>
                )}

                {error && <p className="error-text" role="alert">{error}</p>}

                <button type="submit" className="btn-primary py-2.5" disabled={busy}>
                  {busy ? 'Un momento…' : isForgot ? 'Enviar enlace' : isSignIn ? 'Ingresar' : 'Crear cuenta'}
                </button>

                <button type="button" className="btn-link" onClick={handleGoTo(isSignIn ? 'signup' : 'signin')}>
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

export default AuthPage
