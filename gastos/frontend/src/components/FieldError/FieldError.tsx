interface Props {
  /** The message of the field. Empty (or missing) draws nothing. */
  children?: string
  /** To place it where the form needs it (for example `col-span-2` in a grid). */
  className?: string
}

/**
 * What a form says under a field that has something wrong. It is announced when it appears (role "alert"), and it sits
 * outside the label of the field, so the accessible name of the field stays only its label.
 */
const FieldError = ({ children, className = '' }: Props) => {
  if (!children) return null

  return (
    <p className={`error-text -mt-1 text-sm tracking-normal normal-case ${className}`} role="alert">
      {children}
    </p>
  )
}

export default FieldError
