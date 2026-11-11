import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { api, ApiError } from '../api.ts'
import Brand, { APP_NAME } from '../components/Brand.tsx'
import ThemeToggle from '../components/ThemeToggle.tsx'

/**
 * Where the link of the confirmation mail lands (/confirmar-correo?token=…). It is public: the link may be opened on another
 * device, with no session. The token is used as soon as the page opens. It goes through a query (not an effect) so that
 * React running the page twice in development, or the user coming back to the tab, never spends it twice.
 */
export default function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const token = params.get('token')?.trim() ?? ''

  const { isLoading, isSuccess, error } = useQuery({
    queryKey: ['confirm-email', token],
    // The server answers 204 with no body, and a query may not resolve to undefined: it would count as an error
    queryFn: async () => {
      await api.settings.confirmEmail(token)
      return true
    },
    enabled: token !== '',
    retry: false,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  })

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
          <section className="card w-full max-w-sm gap-4 p-6" aria-live="polite">
            <h1 className="panel-title">Confirmar correo</h1>
            <Result token={token} isLoading={isLoading} isSuccess={isSuccess} error={error} />
            <Link to="/" className="btn-primary py-2.5 text-center">Ir a {APP_NAME}</Link>
          </section>
        </main>
      </div>
    </div>
  )
}

function Result({
  token,
  isLoading,
  isSuccess,
  error,
}: {
  token: string
  isLoading: boolean
  isSuccess: boolean
  error: Error | null
}) {
  if (token === '') {
    return <p className="error-text">Este enlace no trae el código de confirmación. Abrí el enlace completo del correo.</p>
  }
  if (isLoading) return <p className="muted">Confirmando tu correo…</p>
  if (isSuccess) {
    return (
      <p>
        <strong>Correo confirmado.</strong> Ya te vamos a avisar cuando un presupuesto llegue al 80% o lo supere.
      </p>
    )
  }

  // 400: the link expired or was already used. Anything else is a problem of the moment (no connection, too many tries)
  const expired = error instanceof ApiError && error.status === 400
  return (
    <>
      <p className="error-text" role="alert">
        {expired ? 'El enlace venció o ya se usó.' : (error?.message ?? 'No se pudo confirmar el correo.')}
      </p>
      <p className="muted text-sm">
        {expired
          ? 'Entrá a la app y, en la tarjeta Presupuestos, pedí que te reenviemos el correo.'
          : 'Probá de nuevo en unos minutos.'}
      </p>
    </>
  )
}
