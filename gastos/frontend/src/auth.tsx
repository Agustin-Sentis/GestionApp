import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, loadSession, saveSession, setUnauthorizedHandler } from './api.ts'
import type { AuthResponse } from './api.ts'

interface AuthContextValue {
  isAuthenticated: boolean
  signIn: (email: string, password: string) => Promise<void>
  signUp: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [isAuthenticated, setIsAuthenticated] = useState(() => loadSession() !== null)

  const startSession = useCallback((res: AuthResponse) => {
    saveSession({ token: res.token, expiresAtUtc: res.expiresAtUtc })
    setIsAuthenticated(true)
  }, [])

  const logout = useCallback(() => {
    saveSession(null)
    queryClient.clear() // no dejar datos de un usuario en caché para el siguiente
    setIsAuthenticated(false)
  }, [queryClient])

  // Si el backend responde 401 (token vencido o inválido), se cierra la sesión
  useEffect(() => {
    setUnauthorizedHandler(logout)
    return () => setUnauthorizedHandler(null)
  }, [logout])

  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated,
      signIn: async (email, password) => startSession(await api.signIn(email, password)),
      signUp: async (email, password) => startSession(await api.signUp(email, password)),
      logout,
    }),
    [isAuthenticated, startSession, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  return ctx
}
