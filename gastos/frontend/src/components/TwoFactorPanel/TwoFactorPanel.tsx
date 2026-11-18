import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { QRCodeSVG } from 'qrcode.react'
import { useEffect, useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { ApiError, api } from '../../Services/api/api.ts'
import type { RecoveryCodes as RecoveryCodesData, TwoFactorSetup } from '../../Services/api/api.ts'
import { FORM_ERROR_MESSAGE, hasErrors, validateCode, validatePasswordPresent } from '../../shared/Validations/validations.ts'
import type { FormErrors } from '../../shared/Validations/validations.ts'
import FieldError from '../FieldError/FieldError.tsx'
import PasswordField from '../PasswordField/PasswordField.tsx'
import RecoveryCodes from '../RecoveryCodes/RecoveryCodes.tsx'
import { useToast } from '../../Services/toastContext/ToastContext.ts'

type Step =
  | { name: 'idle' }
  | { name: 'password' }
  | { name: 'scan'; setup: TwoFactorSetup }
  | { name: 'confirm'; setup: TwoFactorSetup }
  | { name: 'codes'; codes: string[] }

/** What the user can do once it is on: both ask for the password and a code. */
type Action = 'regenerate' | 'disable'

interface Props {
  /**
   * True while the recovery codes are on screen: they are shown once, so whoever holds this panel (the settings window)
   * must not close it by accident meanwhile.
   */
  onBlockingChange?: (blocking: boolean) => void
}

/**
 * The two-step verification, inside the settings. Off: three steps to turn it on (scan the QR with an authenticator app such
 * as Google Authenticator, confirm the first code, save the recovery codes). On: replace the recovery codes or turn it off.
 * Everything sensitive asks for the password and/or a code; the server says why when something is wrong, and the message is
 * shown next to the fields (never a sign-out: those errors are 400, 409 and 429).
 */
const TwoFactorPanel = ({ onBlockingChange }: Props) => {
  const [step, setStep] = useState<Step>({ name: 'idle' })
  const [action, setAction] = useState<Action | null>(null)
  const [password, setPassword] = useState('')
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  // What is wrong with each field, found before anything is sent
  const [errors, setErrors] = useState<FormErrors<'password' | 'code'>>({})

  const queryClient = useQueryClient()
  const toast = useToast()

  // The same query as the other settings controls: one request, one cache
  const { data: settings } = useQuery({ queryKey: ['settings'], queryFn: api.settings.get })

  const blocking = step.name === 'codes'
  useEffect(() => {
    onBlockingChange?.(blocking)
  }, [blocking, onBlockingChange])

  /** Moves on, leaving nothing typed behind (least of all a password) and no old error. */
  const go = (next: Step, nextAction: Action | null = null) => {
    setStep(next)
    setAction(nextAction)
    setPassword('')
    setCode('')
    setError(null)
    setErrors({})
  }

  const refreshSettings = () => queryClient.invalidateQueries({ queryKey: ['settings'] })

  const start = useMutation({
    mutationFn: (typedPassword: string) => api.twoFactor.setup(typedPassword),
    onSuccess: (setup) => go({ name: 'scan', setup }),
    onError: (e) => setError(e.message),
  })

  const turnOn = useMutation({
    mutationFn: (typedCode: string) => api.twoFactor.enable(typedCode),
    onSuccess: async (result: RecoveryCodesData) => {
      await refreshSettings()
      go({ name: 'codes', codes: result.codes })
    },
    onError: (e) => {
      // 409: there is no activation in progress (the secret was replaced or it was already on): start over, saying so
      if (e instanceof ApiError && e.status === 409) {
        go({ name: 'idle' })
        void refreshSettings()
      }
      setError(e.message)
    },
  })

  const turnOff = useMutation({
    mutationFn: ({ typedPassword, typedCode }: { typedPassword: string; typedCode: string }) =>
      api.twoFactor.disable(typedPassword, typedCode),
    onSuccess: async () => {
      await refreshSettings()
      go({ name: 'idle' })
      toast.success('Verificación en dos pasos desactivada')
    },
    onError: (e) => setError(e.message),
  })

  const regenerate = useMutation({
    mutationFn: ({ typedPassword, typedCode }: { typedPassword: string; typedCode: string }) =>
      api.twoFactor.regenerateRecoveryCodes(typedPassword, typedCode),
    onSuccess: (result: RecoveryCodesData) => go({ name: 'codes', codes: result.codes }),
    onError: (e) => setError(e.message),
  })

  const handleChangePassword = (value: string) => {
    setPassword(value)
    setErrors((previous) => ({ ...previous, password: '' }))
  }

  const handleChangeCode = (e: ChangeEvent<HTMLInputElement>) => {
    setCode(e.target.value)
    setErrors((previous) => ({ ...previous, code: '' }))
  }

  // Shows what is wrong and tells so with the notice every form uses; true when the form can go on
  const checkForm = (formErrors: FormErrors<'password' | 'code'>): boolean => {
    setErrors(formErrors)
    setError(null)
    if (!hasErrors(formErrors)) return true
    toast.error(FORM_ERROR_MESSAGE)
    return false
  }

  const handleSubmitPassword = (event: FormEvent) => {
    event.preventDefault()
    if (!checkForm({ password: validatePasswordPresent(password) })) return
    start.mutate(password)
  }

  const handleSubmitCode = (event: FormEvent) => {
    event.preventDefault()
    if (!checkForm({ code: validateCode(code) })) return
    turnOn.mutate(code.trim())
  }

  const handleSubmitAction = (event: FormEvent) => {
    event.preventDefault()
    if (!checkForm({ password: validatePasswordPresent(password), code: validateCode(code) })) return
    const input = { typedPassword: password, typedCode: code.trim() }
    if (action === 'disable') turnOff.mutate(input)
    else regenerate.mutate(input)
  }

  // Back to the start of the panel: it is what "Cancelar" and "Listo" do
  const handleGoIdle = () => go({ name: 'idle' })

  const handleStartActivation = () => go({ name: 'password' })

  const handleCopySecret = async () => {
    if (step.name !== 'scan') return
    try {
      await navigator.clipboard.writeText(step.setup.secret)
      toast.success('Clave copiada')
    } catch {
      toast.error('No se pudo copiar. Escribila a mano.')
    }
  }

  const handleGoToConfirm = () => {
    if (step.name === 'scan') go({ name: 'confirm', setup: step.setup })
  }

  const handleBackToScan = () => {
    if (step.name === 'confirm') go({ name: 'scan', setup: step.setup })
  }

  const handleStartRegenerate = () => go({ name: 'idle' }, 'regenerate')

  const handleStartDisable = () => go({ name: 'idle' }, 'disable')

  // Nothing to show while the saved state is unknown (still loading, or the request failed)
  if (!settings) return null

  const busy = start.isPending || turnOn.isPending || turnOff.isPending || regenerate.isPending

  const errorLine = error && <p className="error-text" role="alert">{error}</p>

  const passwordField = (
    <PasswordField
      label="Tu contraseña"
      value={password}
      onChange={handleChangePassword}
      autoComplete="current-password"
      error={errors.password}
      autoFocus
    />
  )

  let body
  if (step.name === 'codes') {
    body = <RecoveryCodes codes={step.codes} onDone={handleGoIdle} />
  } else if (step.name === 'password') {
    body = (
      <form className="flex flex-col gap-3" onSubmit={handleSubmitPassword} noValidate>
        <p className="text-sm">Para empezar, confirmá que sos vos con tu contraseña.</p>
        {passwordField}
        {errorLine}
        <div className="flex items-center gap-2">
          <button type="submit" className="btn-primary" disabled={busy}>
            {start.isPending ? 'Un momento…' : 'Continuar'}
          </button>
          <button type="button" className="btn" disabled={busy} onClick={handleGoIdle}>Cancelar</button>
        </div>
      </form>
    )
  } else if (step.name === 'scan') {
    body = (
      <div className="flex flex-col gap-3">
        <p className="text-sm font-bold">Paso 1 de 3 · Escaneá el código QR</p>
        <p className="text-sm">
          Abrí <strong>Google Authenticator</strong> (o cualquier app autenticadora: Authy, Microsoft Authenticator, 1Password…),
          tocá «+» y escaneá este código.
        </p>
        {/* Always dark on white, in the light and the dark theme: that is what the cameras of the apps read */}
        <div role="img" aria-label="Código QR para tu app autenticadora" className="w-fit rounded-sm bg-white p-2">
          <QRCodeSVG value={step.setup.uri} size={176} marginSize={0} bgColor="#ffffff" fgColor="#000000" />
        </div>
        <p className="text-sm">
          ¿No podés escanear? Ingresá esta clave a mano:
          <br />
          <code className="font-mono font-bold break-all">{step.setup.secret}</code>
        </p>
        <div className="flex flex-wrap items-center gap-2">
          <button type="button" className="btn" onClick={handleCopySecret}>
            Copiar clave
          </button>
          <button type="button" className="btn-primary" onClick={handleGoToConfirm}>
            Siguiente
          </button>
          <button type="button" className="btn" onClick={handleGoIdle}>Cancelar</button>
        </div>
      </div>
    )
  } else if (step.name === 'confirm') {
    body = (
      <form className="flex flex-col gap-3" onSubmit={handleSubmitCode} noValidate>
        <p className="text-sm font-bold">Paso 2 de 3 · Confirmá que quedó bien</p>
        <p className="text-sm">Escribí el código de 6 dígitos que muestra la app para DualBalance.</p>
        <label className="field">
          Código de 6 dígitos
          <input
            className="input"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            spellCheck={false}
            value={code}
            onChange={handleChangeCode}
            aria-invalid={errors.code ? true : undefined}
            autoFocus
          />
        </label>
        <FieldError>{errors.code}</FieldError>
        {errorLine}
        <div className="flex items-center gap-2">
          <button type="submit" className="btn-primary" disabled={busy}>
            {turnOn.isPending ? 'Un momento…' : 'Activar'}
          </button>
          <button type="button" className="btn" disabled={busy} onClick={handleBackToScan}>
            Atrás
          </button>
        </div>
      </form>
    )
  } else if (settings.twoFactorEnabled) {
    body = action ? (
      <form className="flex flex-col gap-3" onSubmit={handleSubmitAction} noValidate>
        <p className="text-sm">
          {action === 'disable'
            ? 'Vas a volver a entrar solo con tu contraseña. Se borran la clave y los códigos de recuperación.'
            : 'Los códigos de recuperación que tenés dejan de funcionar y se generan 10 nuevos.'}
        </p>
        {passwordField}
        <label className="field">
          Código de la app o de recuperación
          <input
            className="input"
            type="text"
            autoComplete="one-time-code"
            spellCheck={false}
            value={code}
            onChange={handleChangeCode}
            aria-invalid={errors.code ? true : undefined}
          />
        </label>
        <FieldError>{errors.code}</FieldError>
        {errorLine}
        <div className="flex items-center gap-2">
          <button type="submit" className="btn-primary" disabled={busy}>
            {busy ? 'Un momento…' : action === 'disable' ? 'Desactivar' : 'Generar códigos nuevos'}
          </button>
          <button type="button" className="btn" disabled={busy} onClick={handleGoIdle}>Cancelar</button>
        </div>
      </form>
    ) : (
      <div className="flex flex-col gap-3">
        <p className="text-sm">
          Además de tu contraseña, al entrar se pide el código de tu app autenticadora.
        </p>
        {errorLine}
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
          <button type="button" className="btn" onClick={handleStartRegenerate}>
            Generar códigos de recuperación nuevos
          </button>
          <button type="button" className="btn-link" onClick={handleStartDisable}>
            Desactivar verificación en dos pasos
          </button>
        </div>
      </div>
    )
  } else {
    body = (
      <div className="flex flex-col gap-3">
        <p className="text-sm">
          Sumá una capa de seguridad: además de la contraseña, al entrar se pide un código de tu app autenticadora
          (Google Authenticator, Authy, 1Password…). Es opcional.
        </p>
        {errorLine}
        <button type="button" className="btn-primary self-start" onClick={handleStartActivation}>
          Activar verificación en dos pasos
        </button>
      </div>
    )
  }

  return (
    <section className="flex flex-col gap-3" aria-labelledby="two-factor-title">
      <div className="flex flex-wrap items-center gap-2">
        <h3 id="two-factor-title" className="panel-title text-base">Verificación en dos pasos</h3>
        {settings.twoFactorEnabled && (
          <span className="rounded-sm border-2 border-ink px-2 py-0.5 text-xs font-bold">Activada</span>
        )}
      </div>
      {body}
    </section>
  )
}

export default TwoFactorPanel
