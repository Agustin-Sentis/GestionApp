import { act, fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import ToastProvider from './ToastProvider.tsx'
import { useToast } from './toastContext.ts'

/** Buttons that fire each kind of notice, so the tests drive the provider the way a component would. */
function Trigger({ message = 'Hecho' }: { message?: string }) {
  const toast = useToast()
  return (
    <>
      <button onClick={() => toast.success(message)}>ok</button>
      <button onClick={() => toast.error(message)}>fail</button>
      <button onClick={() => toast.info(message)}>info</button>
    </>
  )
}

function setup() {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
  render(
    <ToastProvider>
      <Trigger />
    </ToastProvider>,
  )

}

describe('ToastProvider', () => {
  it('shows a notice and announces it politely', async () => {
    setup()

    fireEvent.click(screen.getByText('ok'))

    expect(screen.getByRole('status')).toHaveTextContent('Hecho')
  })

  it('announces an error as an alert', async () => {
    setup()

    fireEvent.click(screen.getByText('fail'))

    expect(screen.getByRole('alert')).toHaveTextContent('Hecho')
  })

  it('closes a success notice by itself after 5 seconds, and not before', async () => {
    setup()
    fireEvent.click(screen.getByText('ok'))

    act(() => void vi.advanceTimersByTime(4900))
    expect(screen.getByRole('status')).toBeInTheDocument()

    act(() => void vi.advanceTimersByTime(200))
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('keeps an error on screen longer (8 seconds)', async () => {
    setup()
    fireEvent.click(screen.getByText('fail'))

    act(() => void vi.advanceTimersByTime(7900))
    expect(screen.getByRole('alert')).toBeInTheDocument()

    act(() => void vi.advanceTimersByTime(200))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows an action button, runs it when pressed and closes the notice', () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const onClick = vi.fn()
    function WithAction() {
      const toast = useToast()
      return <button onClick={() => toast.success('Guardado en mayo', { action: { label: 'Ver', onClick } })}>go</button>
    }
    render(
      <ToastProvider>
        <WithAction />
      </ToastProvider>,
    )
    fireEvent.click(screen.getByText('go'))

    fireEvent.click(screen.getByRole('button', { name: 'Ver' }))

    expect(onClick).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('keeps a notice with an action for 8 seconds, so there is time to press it', () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    function WithAction() {
      const toast = useToast()
      return <button onClick={() => toast.success('Guardado', { action: { label: 'Ver', onClick: vi.fn() } })}>go</button>
    }
    render(
      <ToastProvider>
        <WithAction />
      </ToastProvider>,
    )
    fireEvent.click(screen.getByText('go'))

    act(() => void vi.advanceTimersByTime(7900))
    expect(screen.getByRole('button', { name: 'Ver' })).toBeInTheDocument()

    act(() => void vi.advanceTimersByTime(200))
    expect(screen.queryByRole('button', { name: 'Ver' })).not.toBeInTheDocument()
  })

  it('shows no action button when none was given', async () => {
    setup()

    fireEvent.click(screen.getByText('ok'))

    expect(screen.getAllByRole('button', { name: /./ }).map((b) => b.textContent)).not.toContain('Ver')
  })

  it('closes a notice with its close button', async () => {
    setup()
    fireEvent.click(screen.getByText('ok'))

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar aviso' }))

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('closes only the notice whose button was pressed', async () => {
    setup()
    fireEvent.click(screen.getByText('ok'))
    fireEvent.click(screen.getByText('fail'))

    fireEvent.click(screen.getAllByRole('button', { name: 'Cerrar aviso' })[0])

    expect(screen.queryByRole('status')).not.toBeInTheDocument() // the first one (success) is gone
    expect(screen.getByRole('alert')).toBeInTheDocument() // the error is still there
  })

  it('stacks notices and drops the oldest one beyond four', async () => {
    setup()
    for (let i = 0; i < 5; i++) {
      fireEvent.click(screen.getByText(i === 4 ? 'info' : 'ok'))
    }

    expect(screen.getAllByRole('status')).toHaveLength(4)
  })

  it('shows long messages without breaking the layout', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const long = 'x'.repeat(300)
    render(
      <ToastProvider>
        <Trigger message={long} />
      </ToastProvider>,
    )

    fireEvent.click(screen.getByText('ok'))

    expect(screen.getByRole('status')).toHaveTextContent(long)
  })

  it('fails with a clear message when used outside the provider', () => {
    // React logs the error it catches: keep the test output clean
    vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<Trigger />)).toThrow('useToast debe usarse dentro de <ToastProvider>')
  })

  it('does not fire any timer after it is unmounted', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const { unmount } = render(
      <ToastProvider>
        <Trigger />
      </ToastProvider>,
    )
    fireEvent.click(screen.getByText('ok'))

    unmount()

    expect(vi.getTimerCount()).toBe(0)
  })
})
