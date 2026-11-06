import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import Brand, { APP_NAME, BrandMark } from './Brand.tsx'

describe('Brand', () => {
  it('is called DualBalance', () => {
    expect(APP_NAME).toBe('DualBalance')
  })

  it('reads as one word, "DualBalance", even though it is drawn on two lines', () => {
    const { container } = render(<Brand />)

    // No space between the two lines: a screen reader says "DualBalance", not "Dual Balance"
    expect(container.textContent).toBe('DualBalance')
    expect(screen.getByText('Dual')).toBeInTheDocument()
    expect(screen.getByText('Balance')).toBeInTheDocument()
  })

  it('is the same name that the app\'s own constant says, so the page cannot call itself two things', () => {
    const { container } = render(<Brand />)

    expect(container.textContent).toBe(APP_NAME)
  })

  it('draws the mark as decoration: the name carries the meaning', () => {
    const { container } = render(<BrandMark />)

    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
  })

  it('takes extra classes, to hide it where the margin already shows it', () => {
    const { container } = render(<Brand className="xl:hidden" />)

    expect(container.firstChild).toHaveClass('xl:hidden')
  })
})
