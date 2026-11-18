import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import PasswordField from './PasswordField.tsx'

function Harness({ hint }: { hint?: string }) {
  const [value, setValue] = useState('')
  return (
    <PasswordField label="Contraseña" value={value} onChange={setValue} autoComplete="current-password" placeholder="Tu contraseña">
      {hint && <small>{hint}</small>}
    </PasswordField>
  )
}

describe('PasswordField', () => {
  it('starts hidden, and the accessible name of the input is only its label', () => {
    render(<Harness />)

    const input = screen.getByLabelText('Contraseña')
    expect(input).toHaveAttribute('type', 'password')
    expect(input).toHaveAttribute('placeholder', 'Tu contraseña')
    expect(screen.getByRole('button', { name: 'Mostrar contraseña' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('shows and hides what was typed, without losing it', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    const input = screen.getByLabelText('Contraseña')
    await user.type(input, 'Demo1234!')

    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(input).toHaveAttribute('type', 'text')
    expect(input).toHaveValue('Demo1234!')
    expect(screen.getByRole('button', { name: 'Mostrar contraseña' })).toHaveAttribute('aria-pressed', 'true')

    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(input).toHaveAttribute('type', 'password')
    expect(input).toHaveValue('Demo1234!')
  })

  it('the button does not submit a form', async () => {
    const user = userEvent.setup()
    let submitted = false
    render(
      <form onSubmit={(e) => { e.preventDefault(); submitted = true }}>
        <Harness />
      </form>,
    )

    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(submitted).toBe(false)
  })

  it('writes the error under the field and marks it invalid, without changing the name of the field', () => {
    render(
      <PasswordField label="Contraseña" value="" onChange={() => {}} autoComplete="current-password" error="La contraseña es obligatoria." />,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('La contraseña es obligatoria.')
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('aria-invalid', 'true')
  })

  it('has no error mark when there is no error', () => {
    render(<PasswordField label="Contraseña" value="" onChange={() => {}} autoComplete="current-password" error="" />)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Contraseña')).not.toHaveAttribute('aria-invalid')
  })

  it('shows what is passed under the input', () => {
    render(<Harness hint="Mínimo 8 caracteres" />)

    expect(screen.getByText('Mínimo 8 caracteres')).toBeInTheDocument()
  })
})
