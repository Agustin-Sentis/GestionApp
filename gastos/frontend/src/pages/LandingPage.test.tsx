import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { formatMoney } from '../format.ts'
import { CAPABILITIES, LATER, SAMPLE_LINES, sampleTotals } from '../landingContent.ts'
import { renderWithProviders } from '../test/render.tsx'
import LandingPage from './LandingPage.tsx'

describe('LandingPage', () => {
  it('opens with what the app is for', () => {
    renderWithProviders(<LandingPage />)

    const heading = screen.getByRole('heading', { level: 1 })
    expect(heading).toHaveTextContent('Anotá lo que gastás.')
    expect(heading).toHaveTextContent('Sabé lo que te queda.')
  })

  it('sends new people to create an account and returning ones to sign in', () => {
    renderWithProviders(<LandingPage />)

    const create = screen.getAllByRole('link', { name: 'Crear cuenta' })
    expect(create.length).toBeGreaterThanOrEqual(2) // the top bar and the foot of the page
    create.forEach((link) => expect(link).toHaveAttribute('href', '/login?modo=registro'))
    expect(screen.getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', '/login')
    expect(screen.getByRole('link', { name: 'Ya tengo cuenta' })).toHaveAttribute('href', '/login')
  })

  it('lists everything the app does, each with its explanation', () => {
    renderWithProviders(<LandingPage />)

    const section = screen.getByRole('region', { name: 'Qué podés hacer' })
    const terms = within(section).getAllByRole('term').map((term) => term.textContent)

    expect(terms).toEqual(CAPABILITIES.map((item) => item.concept))
    for (const item of CAPABILITIES) {
      expect(within(section).getByText(item.what)).toBeInTheDocument()
    }
  })

  it('does not promise anything that is not there: every capability has a concept and a real sentence', () => {
    for (const item of CAPABILITIES) {
      expect(item.concept.trim()).not.toBe('')
      expect(item.what.trim().length).toBeGreaterThan(30)
      expect(item.what).not.toMatch(/próximamente|pronto|coming soon/i)
    }
  })

  it('writes the example sheet and adds it up correctly', () => {
    renderWithProviders(<LandingPage />)

    const sheet = screen.getByRole('figure', { name: 'Ejemplo de una hoja del libro' })
    const rows = within(sheet).getAllByRole('row')
    expect(rows).toHaveLength(SAMPLE_LINES.length)
    for (const line of SAMPLE_LINES) {
      expect(within(sheet).getByText(line.concept, { exact: false })).toBeInTheDocument()
    }

    // The money format uses a non-breaking space, which Testing Library turns into a normal one in the page but not in the pattern
    const money = (value: number) =>
      new RegExp(formatMoney(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/\s/g, '\\s'))
    const { income, expense, left } = sampleTotals()
    expect(within(sheet).getAllByText(money(income)).length).toBeGreaterThan(0) // the total, and the Sueldo line
    expect(within(sheet).getByText(money(expense))).toBeInTheDocument()
    expect(within(sheet).getByText(money(left))).toBeInTheDocument()
  })

  it('has an example whose totals really are the sum of its lines', () => {
    const { income, expense, left } = sampleTotals()

    expect(income).toBe(900000)
    expect(expense).toBe(48200 + 310000 + 12 * 1300)
    expect(left).toBe(income - expense)
    expect(left).toBeGreaterThan(0)
  })

  it('shows the USD line with the rate it was converted with', () => {
    renderWithProviders(<LandingPage />)

    expect(screen.getByText(/cotización del día/)).toBeInTheDocument()
  })

  it('lists what is still to come, apart from what exists', () => {
    renderWithProviders(<LandingPage />)

    const section = screen.getByRole('region', { name: 'Anotado para más adelante' })
    expect(within(section).getAllByRole('listitem')).toHaveLength(LATER.length)
  })

  it('lets the visitor pick light or chalkboard before signing in', () => {
    renderWithProviders(<LandingPage />)

    expect(screen.getByRole('group', { name: 'Tema' })).toBeInTheDocument()
  })

  it('signs the page with the author\'s name and where it was made', () => {
    renderWithProviders(<LandingPage />)

    const footer = screen.getByRole('contentinfo')
    expect(within(footer).getByText('Agustín Sentis')).toBeInTheDocument()
    expect(within(footer).getByText(/Rosario, Argentina/)).toBeInTheDocument()
  })

  it('links to the author\'s profile and the API docs, and opens them in another tab safely', () => {
    renderWithProviders(<LandingPage />)

    const links = within(screen.getByRole('navigation', { name: 'Enlaces del proyecto' }))
    const profile = links.getByRole('link', { name: 'GitHub' })
    const docs = links.getByRole('link', { name: 'Documentación de la API' })

    expect(profile).toHaveAttribute('href', 'https://github.com/Agustin-Sentis')
    expect(docs).toHaveAttribute('href', 'https://gestionapp-j0j4.onrender.com/scalar')
    for (const link of [profile, docs]) {
      expect(link).toHaveAttribute('target', '_blank')
      expect(link.getAttribute('rel')).toContain('noreferrer')
    }
  })
})
