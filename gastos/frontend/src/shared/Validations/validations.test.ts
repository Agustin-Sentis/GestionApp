import { describe, expect, it } from 'vitest'
import {
  FORM_ERROR_MESSAGE,
  hasErrors,
  validateAmount,
  validateBudget,
  validateCategoryId,
  validateCategoryName,
  validateCode,
  validateDate,
  validateEmail,
  validateEmailPresent,
  validateNewPassword,
  validateNote,
  validatePasswordPresent,
  validateRepeatPassword,
} from './validations.ts'

describe('hasErrors', () => {
  it('is false when every field is empty or missing, and true when one has a message', () => {
    expect(hasErrors({})).toBe(false)
    expect(hasErrors({ email: '', password: undefined })).toBe(false)
    expect(hasErrors({ email: '', password: 'Falta' })).toBe(true)
  })

  it('has one notice for every form', () => {
    expect(FORM_ERROR_MESSAGE).toBe('Hay algunos campos incorrectos, revisalos.')
  })
})

describe('email', () => {
  it('sign in only asks that there is one, of a sane size', () => {
    expect(validateEmailPresent('')).toBe('El email es obligatorio.')
    expect(validateEmailPresent('   ')).toBe('El email es obligatorio.')
    expect(validateEmailPresent('x'.repeat(255))).toBe('El email no puede superar los 254 caracteres.')
    // An account created earlier with an odd address must still get in: the shape is not judged here
    expect(validateEmailPresent('sin-arroba')).toBe('')
  })

  it('a new address needs the shape of an email', () => {
    expect(validateEmail('ana@example.com')).toBe('')
    expect(validateEmail('  ana@example.com  ')).toBe('')
    expect(validateEmail('')).toBe('El email es obligatorio.')
    for (const wrong of ['ana', 'ana@', '@example.com', 'ana@example', 'a na@example.com', 'ana@@example.com']) {
      expect(validateEmail(wrong), wrong).toBe('El formato del email no es válido.')
    }
  })

  it('too long is said before the shape', () => {
    expect(validateEmail(`${'a'.repeat(250)}@example.com`)).toBe('El email no puede superar los 254 caracteres.')
  })
})

describe('passwords', () => {
  it('asks only that a current password is there, and of a sane size', () => {
    expect(validatePasswordPresent('')).toBe('La contraseña es obligatoria.')
    expect(validatePasswordPresent('a')).toBe('')
    expect(validatePasswordPresent(' ')).toBe('') // a password may be spaces: it is not trimmed
    expect(validatePasswordPresent('a'.repeat(101))).toBe('La contraseña no puede superar los 100 caracteres.')
  })

  it('a new password follows the rules of the API, one message at a time, in its order', () => {
    expect(validateNewPassword('')).toBe('La contraseña es obligatoria.')
    expect(validateNewPassword('Ab1!')).toBe('La contraseña debe tener al menos 8 caracteres.')
    expect(validateNewPassword('a'.repeat(101))).toBe('La contraseña no puede superar los 100 caracteres.')
    expect(validateNewPassword('abcdefg1!')).toBe('La contraseña debe contener al menos una letra mayúscula.')
    expect(validateNewPassword('ABCDEFG1!')).toBe('La contraseña debe contener al menos una letra minúscula.')
    expect(validateNewPassword('Abcdefgh!')).toBe('La contraseña debe contener al menos un número.')
    expect(validateNewPassword('Abcdefg12')).toBe('La contraseña debe contener al menos un carácter especial.')
    expect(validateNewPassword('Demo1234!')).toBe('')
  })

  it('accepts exactly 8 and exactly 100 characters', () => {
    expect(validateNewPassword('Abcde1!x')).toBe('')
    expect(validateNewPassword(`Ab1!${'x'.repeat(96)}`)).toBe('')
  })

  it('the repeated password has to be there and match', () => {
    expect(validateRepeatPassword('Demo1234!', '')).toBe('Repetí la contraseña.')
    expect(validateRepeatPassword('Demo1234!', 'Demo1234')).toBe('Las contraseñas no coinciden.')
    expect(validateRepeatPassword('Demo1234!', 'Demo1234!')).toBe('')
  })
})

describe('codes of the two-step verification', () => {
  it('has to be there, and not be absurdly long', () => {
    expect(validateCode('')).toBe('El código es obligatorio.')
    expect(validateCode('   ')).toBe('El código es obligatorio.')
    expect(validateCode('x'.repeat(21))).toBe('El código no es válido.')
    expect(validateCode('123456')).toBe('')
    expect(validateCode('ABCD-EFGH-JK')).toBe('')
  })
})

describe('amounts', () => {
  it('needs a number above zero, up to a thousand million', () => {
    expect(validateAmount('')).toBe('El monto es obligatorio.')
    expect(validateAmount('abc')).toBe('El monto no es válido.')
    expect(validateAmount('0')).toBe('El monto debe ser mayor a cero.')
    expect(validateAmount('-5')).toBe('El monto debe ser mayor a cero.')
    expect(validateAmount('1000000001')).toBe('El monto no puede superar los 1.000.000.000.')
    expect(validateAmount('1000000000')).toBe('')
    expect(validateAmount('0.01')).toBe('')
    expect(validateAmount('1500.5')).toBe('')
  })

  it('does not take Infinity or NaN as numbers', () => {
    expect(validateAmount('Infinity')).toBe('El monto no es válido.')
    expect(validateAmount('NaN')).toBe('El monto no es válido.')
  })

  it('a budget has the same limits, in its own words', () => {
    expect(validateBudget('')).toBe('El presupuesto es obligatorio.')
    expect(validateBudget('0')).toBe('El presupuesto debe ser mayor a cero.')
    expect(validateBudget('x')).toBe('El presupuesto no es válido.')
    expect(validateBudget('1000000001')).toBe('El presupuesto no puede superar los 1.000.000.000.')
    expect(validateBudget('32462')).toBe('')
  })
})

describe('the rest of a movement', () => {
  it('the date has to be there and be a real day', () => {
    expect(validateDate('')).toBe('La fecha es obligatoria.')
    expect(validateDate('17/11/2026')).toBe('La fecha no es válida.')
    expect(validateDate('2026-02-31')).toBe('La fecha no es válida.')
    expect(validateDate('2026-11-17')).toBe('')
  })

  it('the note is optional, up to 200 characters', () => {
    expect(validateNote('')).toBe('')
    expect(validateNote('x'.repeat(200))).toBe('')
    expect(validateNote('x'.repeat(201))).toBe('La nota no puede superar los 200 caracteres.')
  })

  it('a category has to be chosen', () => {
    expect(validateCategoryId('')).toBe('La categoría es obligatoria.')
    expect(validateCategoryId('3f2b1c9e')).toBe('')
  })

  it('a category name has to be there (not only spaces), up to 60 characters', () => {
    expect(validateCategoryName('')).toBe('El nombre de la categoría es obligatorio.')
    expect(validateCategoryName('   ')).toBe('El nombre de la categoría es obligatorio.')
    expect(validateCategoryName('x'.repeat(60))).toBe('')
    expect(validateCategoryName(`  ${'x'.repeat(60)}  `)).toBe('') // the spaces around are not counted: they are trimmed when sent
    expect(validateCategoryName('x'.repeat(61))).toBe('El nombre de la categoría no puede superar los 60 caracteres.')
  })
})
