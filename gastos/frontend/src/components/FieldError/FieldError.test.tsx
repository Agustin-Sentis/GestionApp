import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import FieldError from './FieldError.tsx'

describe('FieldError', () => {
  it('shows the message as an alert', () => {
    render(<FieldError>El monto es obligatorio.</FieldError>)

    expect(screen.getByRole('alert')).toHaveTextContent('El monto es obligatorio.')
  })

  it('draws nothing when there is no message', () => {
    const { container, rerender } = render(<FieldError>{''}</FieldError>)
    expect(container).toBeEmptyDOMElement()

    rerender(<FieldError />)
    expect(container).toBeEmptyDOMElement()
  })
})
