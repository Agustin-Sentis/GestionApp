import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from './auth.tsx'
import RequireAuth from './components/RequireAuth.tsx'
import AuthPage from './pages/AuthPage.tsx'
import ConfirmEmailPage from './pages/ConfirmEmailPage.tsx'
import DashboardPage from './pages/DashboardPage.tsx'
import LandingPage from './pages/LandingPage.tsx'
import ResetPasswordPage from './pages/ResetPasswordPage.tsx'
import { PANEL_PATH, returnPathFrom } from './returnPath.ts'

/** Whoever is already signed in has no use for the sign-in form: back to the page they were going to, or to the dashboard. */
function PublicOnly({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  return isAuthenticated ? <Navigate to={returnPathFrom(location.state)} replace /> : children
}

/**
 * The root address shows the landing page to someone who has not signed in. Someone who has goes to the dashboard (/panel),
 * keeping the query string: old links to the dashboard (with ?mes=…) keep working.
 */
function Home() {
  const { isAuthenticated } = useAuth()
  const { search } = useLocation()
  return isAuthenticated ? <Navigate to={`${PANEL_PATH}${search}`} replace /> : <LandingPage />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<PublicOnly><AuthPage /></PublicOnly>} />
      {/* Public and open to anyone: the link of the confirmation mail may be opened on another device */}
      <Route path="/confirmar-correo" element={<ConfirmEmailPage />} />
      <Route path="/restablecer-contrasena" element={<ResetPasswordPage />} />
      <Route path={PANEL_PATH} element={<RequireAuth><DashboardPage /></RequireAuth>} />
      <Route path="/" element={<Home />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
