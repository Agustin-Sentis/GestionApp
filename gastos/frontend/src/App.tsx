import { Navigate, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from './auth.tsx'
import AuthPage from './pages/AuthPage.tsx'
import ConfirmEmailPage from './pages/ConfirmEmailPage.tsx'
import DashboardPage from './pages/DashboardPage.tsx'
import LandingPage from './pages/LandingPage.tsx'
import ResetPasswordPage from './pages/ResetPasswordPage.tsx'

function PublicOnly({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <Navigate to="/" replace /> : children
}

/**
 * The same address shows two things: the landing page to someone who has not signed in, and their dashboard once they have.
 * That way existing links to the dashboard (with ?mes=…) keep working, and signing in needs no redirect.
 */
function Home() {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <DashboardPage /> : <LandingPage />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<PublicOnly><AuthPage /></PublicOnly>} />
      {/* Public and open to anyone: the link of the confirmation mail may be opened on another device */}
      <Route path="/confirmar-correo" element={<ConfirmEmailPage />} />
      <Route path="/restablecer-contrasena" element={<ResetPasswordPage />} />
      <Route path="/" element={<Home />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
