import { parseDateValue } from '../format.ts'

/**
 * The rules of every form, in one place. Each validator receives what was typed and returns the message to show,
 * or an empty text when the value is fine (the same convention the course project follows). They repeat the rules of
 * the API (GastosBackend.Application/Validations), with the same words: they are there so the person finds out at
 * once and nothing invalid is sent, but the API stays the one that decides.
 */

/** What every form shows (as a notice) when it is sent with something wrong. */
export const FORM_ERROR_MESSAGE = 'Hay algunos campos incorrectos, revisalos.'

export const MAX_EMAIL_LENGTH = 254
export const MAX_PASSWORD_LENGTH = 100
export const MAX_CODE_LENGTH = 20
export const MAX_AMOUNT = 1_000_000_000
export const MAX_NOTE_LENGTH = 200
export const MAX_CATEGORY_NAME_LENGTH = 60

const REGEX_EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** The errors of a form: the message of each field that has one, and an empty text (or nothing) for the rest. */
export type FormErrors<Field extends string> = Partial<Record<Field, string>>

/** True when some field of the form has an error. */
export const hasErrors = (errors: Record<string, string | undefined>): boolean => Object.values(errors).some((message) => !!message)

/** Is there a text at all? A text of only spaces is nothing. */
const isBlank = (value: string): boolean => value.trim() === ''

/* Email */

/** Only that there is one, of a sane size: sign in does not judge the format (accounts created earlier must still get in). */
export const validateEmailPresent = (value: string): string => {
  if (isBlank(value)) return 'El email es obligatorio.'
  if (value.length > MAX_EMAIL_LENGTH) return `El email no puede superar los ${MAX_EMAIL_LENGTH} caracteres.`
  return ''
}

/** For a new address (sign up, recover, change): present, of a sane size and with the shape of an email. */
export const validateEmail = (value: string): string => {
  const present = validateEmailPresent(value)
  if (present) return present
  if (!REGEX_EMAIL.test(value.trim())) return 'El formato del email no es válido.'
  return ''
}

/* Passwords */

/** Only that there is one, of a sane size: whether it is the right one is the API's business. */
export const validatePasswordPresent = (value: string): string => {
  if (value === '') return 'La contraseña es obligatoria.'
  if (value.length > MAX_PASSWORD_LENGTH) return `La contraseña no puede superar los ${MAX_PASSWORD_LENGTH} caracteres.`
  return ''
}

/** The rules of a NEW password (sign up and reset): the same text PasswordRulesHint shows. */
export const validateNewPassword = (value: string): string => {
  if (value === '') return 'La contraseña es obligatoria.'
  if (value.length < 8) return 'La contraseña debe tener al menos 8 caracteres.'
  if (value.length > MAX_PASSWORD_LENGTH) return `La contraseña no puede superar los ${MAX_PASSWORD_LENGTH} caracteres.`
  if (!/[A-Z]/.test(value)) return 'La contraseña debe contener al menos una letra mayúscula.'
  if (!/[a-z]/.test(value)) return 'La contraseña debe contener al menos una letra minúscula.'
  if (!/[0-9]/.test(value)) return 'La contraseña debe contener al menos un número.'
  if (!/[^a-zA-Z0-9]/.test(value)) return 'La contraseña debe contener al menos un carácter especial.'
  return ''
}

/** The second time the new password is typed: it has to be the same as the first. */
export const validateRepeatPassword = (password: string, repeat: string): string => {
  if (repeat === '') return 'Repetí la contraseña.'
  if (repeat !== password) return 'Las contraseñas no coinciden.'
  return ''
}

/* Codes of the two-step verification */

/** A code of the app (6 digits) or a recovery one (11 characters): here only that there is one, of a sane size. */
export const validateCode = (value: string): string => {
  if (isBlank(value)) return 'El código es obligatorio.'
  if (value.length > MAX_CODE_LENGTH) return 'El código no es válido.'
  return ''
}

/* Movements */

/** An amount typed in a field (as text): a number above zero, up to the API's limit. */
const validateMoney = (value: string, name: string, limitMessage: string): string => {
  if (isBlank(value)) return `${name} es obligatorio.`
  const amount = Number(value)
  if (!Number.isFinite(amount)) return `${name} no es válido.`
  if (amount <= 0) return `${name} debe ser mayor a cero.`
  if (amount > MAX_AMOUNT) return limitMessage
  return ''
}

export const validateAmount = (value: string): string => validateMoney(value, 'El monto', 'El monto no puede superar los 1.000.000.000.')

export const validateBudget = (value: string): string =>
  validateMoney(value, 'El presupuesto', 'El presupuesto no puede superar los 1.000.000.000.')

/** The date of a movement, as yyyy-MM-dd. */
export const validateDate = (value: string): string => {
  if (value === '') return 'La fecha es obligatoria.'
  if (!parseDateValue(value)) return 'La fecha no es válida.'
  return ''
}

export const validateNote = (value: string): string => {
  if (value.length > MAX_NOTE_LENGTH) return `La nota no puede superar los ${MAX_NOTE_LENGTH} caracteres.`
  return ''
}

export const validateCategoryId = (value: string): string => {
  if (value === '') return 'La categoría es obligatoria.'
  return ''
}

export const validateCategoryName = (value: string): string => {
  if (isBlank(value)) return 'El nombre de la categoría es obligatorio.'
  if (value.trim().length > MAX_CATEGORY_NAME_LENGTH) {
    return `El nombre de la categoría no puede superar los ${MAX_CATEGORY_NAME_LENGTH} caracteres.`
  }
  return ''
}
