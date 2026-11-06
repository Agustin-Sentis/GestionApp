import { Component } from 'react'
import type { ErrorInfo, ReactNode } from 'react'

interface Props {
  children: ReactNode
  /** What the button does. Reloads the page by default; a test passes its own. */
  onReload?: () => void
}

interface State {
  failed: boolean
}

/**
 * Catches an error thrown while rendering any component below it, so a bug shows a clear message
 * instead of a blank page. It does not catch errors from event handlers or from requests: those are
 * handled where they happen (the forms and the notices).
 *
 * The screen is plain markup on purpose: it cannot depend on the providers, because they may be the thing that broke.
 */
export default class ErrorBoundary extends Component<Props, State> {
  state: State = { failed: false }

  static getDerivedStateFromError(): State {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // The technical detail is for whoever debugs it, not for the person using the app
    console.error('La aplicación tuvo un error inesperado', error, info.componentStack)
  }

  render() {
    if (!this.state.failed) return this.props.children

    const reload = this.props.onReload ?? (() => window.location.reload())

    return (
      <main className="grid min-h-screen place-items-center p-4">
        <div className="card w-full max-w-sm gap-4 p-6" role="alert">
          <div>
            <h1 className="panel-title">Algo salió mal</h1>
            <p className="muted mt-3 text-sm">
              La página tuvo un error inesperado. Lo que ya guardaste no se pierde: recargá para volver a intentarlo.
            </p>
          </div>
          <button type="button" className="btn-primary" onClick={reload}>
            Recargar la página
          </button>
        </div>
      </main>
    )
  }
}
