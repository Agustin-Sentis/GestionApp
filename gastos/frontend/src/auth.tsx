import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, loadSession, saveSession, setUnauthorizedHandler } from './api.ts'
import type { AuthResponse, TwoFactorChallenge } from './api.ts'

interface AuthContextValue {
  isAuthenticated: boolean
  /**
   * Starts the session, unless the account has the two-step verification: then there is none yet and the challenge comes
   * back, to be finished with {@link verifyTwoFactor}. Null means the session started.
   */
  signIn: (email: string, password: string) => Promise<TwoFactorChallenge | null>
  /** The second step of the sign in: the challenge and a code (of the app, or a recovery one) start the session. */
  verifyTwoFactor: (challengeToken: string, code: string) => Promise<void>
  signUp: (email: string, password: string) => Promise<void>
  /** Closes the session (also what the API's 401 does). The guard sends the visitor to the sign-in form. */
  logout: () => void
  /** The user chose to leave ("Salir"): closes the session and the guard sends the visitor to the landing page instead. */
  signOut: () => void
  /** True from the moment the user chose to leave until the next session starts. Read by the guard. */
  signedOutByChoice: boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [isAuthenticated, setIsAuthenticated] = useState(() => loadSession() !== null)

  const [signedOutByChoice, setSignedOutByChoice] = useState(false)

  const startSession = useCallback((res: AuthResponse) => {
    saveSession({ token: res.token, expiresAtUtc: res.expiresAtUtc })
    setSignedOutByChoice(false)
    setIsAuthenticated(true)
  }, [])

  const logout = useCallback(() => {
    saveSession(null)
    queryClient.clear() // no dejar datos de un usuario en caché para el siguiente
    setIsAuthenticated(false)
  }, [queryClient])

  const signOut = useCallback(() => {
    setSignedOutByChoice(true)
    logout()
  }, [logout])

  // Si el backend responde 401 (token vencido o inválido), se cierra la sesión
  useEffect(() => {
    setUnauthorizedHandler(logout)
    return () => setUnauthorizedHandler(null)
  }, [logout])

  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated,
      signIn: async (email, password) => {
        const result = await api.signIn(email, password)
        if (result.requiresTwoFactor) return result
        startSession(result)
        return null
      },
      verifyTwoFactor: async (challengeToken, code) => startSession(await api.verifyTwoFactor(challengeToken, code)),
      signUp: async (email, password) => startSession(await api.signUp(email, password)),
      logout,
      signOut,
      signedOutByChoice,
    }),
    [isAuthenticated, startSession, logout, signOut, signedOutByChoice],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  return ctx
}
