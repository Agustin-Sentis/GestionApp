import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ErrorBoundary from './ErrorBoundary.tsx'

function Broken(): never {
  throw new Error('secret internal detail: db password is hunter2')
}

describe('ErrorBoundary', () => {
  beforeEach(() => {
    // React and the boundary both log the error they catch: keep the test output readable
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  it('renders its children when nothing fails', () => {
    render(
      <ErrorBoundary>
        <p>todo bien</p>
      </ErrorBoundary>,
    )

    expect(screen.getByText('todo bien')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('replaces a component that fails to render with a clear message', () => {
    render(
      <ErrorBoundary>
        <p>contenido</p>
        <Broken />
      </ErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('Algo salió mal')
    expect(screen.queryByText('contenido')).not.toBeInTheDocument()
  })

  it('does not show the technical detail to the user, only to the console', () => {
    render(
      <ErrorBoundary>
        <Broken />
      </ErrorBoundary>,
    )

    expect(screen.queryByText(/hunter2/)).not.toBeInTheDocument()
    expect(console.error).toHaveBeenCalledWith(
      'La aplicación tuvo un error inesperado',
      expect.objectContaining({ message: expect.stringContaining('hunter2') }),
      expect.any(String),
    )
  })

  it('offers a button that reloads the page', async () => {
    const onReload = vi.fn()
    render(
      <ErrorBoundary onReload={onReload}>
        <Broken />
      </ErrorBoundary>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Recargar la página' }))

    expect(onReload).toHaveBeenCalledTimes(1)
  })

  it('catches a failure deep in the tree, not only in its direct child', () => {
    render(
      <ErrorBoundary>
        <section>
          <div>
            <Broken />
          </div>
        </section>
      </ErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toBeInTheDocument()
  })
})
