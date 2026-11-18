import { createContext, useContext } from 'react'
import type { TwoFactorChallenge } from '../api/api.ts'

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

const useAuth = (): AuthContextValue => {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthContextProvider>')
  return ctx
}

export type { AuthContextValue }
export { AuthContext, useAuth }
