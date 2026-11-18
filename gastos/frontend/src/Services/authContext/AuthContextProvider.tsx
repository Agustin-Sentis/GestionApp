import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, loadSession, saveSession, setUnauthorizedHandler } from '../api/api.ts'
import type { AuthResponse } from '../api/api.ts'
import { AuthContext } from './AuthContext.ts'
import type { AuthContextValue } from './AuthContext.ts'

const AuthContextProvider = ({ children }: { children: ReactNode }) => {
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

export default AuthContextProvider
