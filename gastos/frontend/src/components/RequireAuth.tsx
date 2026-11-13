import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from '../auth.tsx'

/**
 * Guard of the private pages. Without a session the visitor goes to the sign-in form, and the page they wanted (with its month
 * and filters) travels in the navigation state, so signing in brings them back to it. This is about the experience: what really
 * protects the data is the API, which answers 401 to anyone without a valid session.
 */
export default function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated, signedOutByChoice } = useAuth()
  const location = useLocation()

  if (isAuthenticated) return children
  // Chose to leave: the landing page, not a form asking to come back in
  if (signedOutByChoice) return <Navigate to="/" replace />
  return <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />
}
