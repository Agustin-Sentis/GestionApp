import { focusManager } from '@tanstack/react-query'
import { screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { loadSession, saveSession } from './api.ts'
import { useAuth } from './auth.tsx'
import { mockApi } from './test/fetchMock.ts'
import { settings } from './test/fixtures.ts'
import { renderWithProviders } from './test/render.tsx'
import { useSessionWatch } from './useSessionWatch.ts'

function Probe() {
  useSessionWatch()
  const { isAuthenticated } = useAuth()
  return <p>{isAuthenticated ? 'dentro' : 'fuera'}</p>
}

/** The user leaves the tab and comes back to it. */
function comeBackToTheTab() {
  focusManager.setFocused(false)
  focusManager.setFocused(true)
}

beforeEach(() => {
  localStorage.clear()
  saveSession({ token: 'jwt', expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString() })
})

describe('useSessionWatch', () => {
  it('signs out when the user comes back to the tab and the server closed the session meanwhile', async () => {
    let open = true
    const fetchMock = mockApi({
      'GET /api/settings': () => (open ? { body: settings() } : { status: 401, body: { error: 'No autorizado' } }),
    })
    renderWithProviders(<Probe />)
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    expect(screen.getByText('dentro')).toBeInTheDocument()

    open = false // the password was reset in another browser
    comeBackToTheTab()

    expect(await screen.findByText('fuera')).toBeInTheDocument()
    expect(loadSession()).toBeNull()
  })

  it('stays signed in, and asks again, when the session is still good', async () => {
    const fetchMock = mockApi({ 'GET /api/settings': () => ({ body: settings() }) })
    renderWithProviders(<Probe />)
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))

    comeBackToTheTab()

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    expect(screen.getByText('dentro')).toBeInTheDocument()
    expect(loadSession()).not.toBeNull()
  })

  it('does not sign out when the server is unreachable: a failed connection is not a closed session', async () => {
    let up = true
    const fetchMock = mockApi({
      'GET /api/settings': () => (up ? { body: settings() } : { status: 503, body: { error: 'caído' } }),
    })
    renderWithProviders(<Probe />)
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))

    up = false
    comeBackToTheTab()

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    expect(screen.getByText('dentro')).toBeInTheDocument()
  })
})
