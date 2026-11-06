import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { renderWithProviders } from '../test/render.tsx'
import AuthPage from './AuthPage.tsx'

describe('AuthPage', () => {
  it('starts on the sign-in form', () => {
    renderWithProviders(<AuthPage />)

    expect(screen.getByText('Ingresá a tu cuenta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ingresar' })).toBeInTheDocument()
  })

  it('switches to the sign-up form and shows the password rules', async () => {
    renderWithProviders(<AuthPage />)

    await userEvent.click(screen.getByRole('button', { name: '¿No tenés cuenta? Registrate' }))

    expect(screen.getByText('Creá tu cuenta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeInTheDocument()
    expect(screen.getByText(/Mínimo 8 caracteres/)).toBeInTheDocument()
  })

  it('opens on the sign-up form when the landing page asks for a new account', () => {
    renderWithProviders(<AuthPage />, { route: '/login?modo=registro' })

    expect(screen.getByText('Creá tu cuenta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeInTheDocument()
  })

  it('opens on sign-in for any other value of the parameter', () => {
    renderWithProviders(<AuthPage />, { route: '/login?modo=cualquiera' })

    expect(screen.getByText('Ingresá a tu cuenta')).toBeInTheDocument()
  })

  it('leads back to the landing page', () => {
    renderWithProviders(<AuthPage />)

    expect(screen.getByRole('link', { name: '← Volver al inicio' })).toHaveAttribute('href', '/')
  })
})
