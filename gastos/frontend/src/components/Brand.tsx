/**
 * The mark: a page of the notebook. A square sheet, the double red margin line and a blue stroke going up, as if the
 * line of the month had been drawn with a ballpoint. It takes its colors from the theme, so it turns to chalk on the dark side.
 */
export function BrandMark({ className = 'size-8' }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" aria-hidden="true" className={className}>
      <rect x="1" y="1" width="30" height="30" rx="3" fill="var(--sheet)" stroke="var(--ink)" strokeWidth="2" />
      <path d="M3 12h26M3 22h26" stroke="var(--line)" strokeWidth="1" />
      <path d="M8 2v28M11 2v28" stroke="var(--margin)" strokeWidth="1.4" />
      <path
        d="M15 22l4-5 3 3 5-8"
        fill="none"
        stroke="var(--pen)"
        strokeWidth="2.4"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

/** The mark and the name, side by side. */
export default function Brand({ className = '' }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2.5 ${className}`}>
      <BrandMark />
      <span className="display text-lg uppercase" style={{ letterSpacing: '0.04em' }}>
        Cuentas
      </span>
    </span>
  )
}
